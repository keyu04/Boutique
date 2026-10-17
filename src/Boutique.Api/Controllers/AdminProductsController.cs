using System.Text.RegularExpressions;
using Boutique.Application.Dtos;
using Boutique.Application.Interfaces;
using Boutique.Domain;
using Boutique.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Boutique.Api.Controllers;

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = "Admin,Staff")] // read access for both; write actions are tightened to Admin below
public class AdminProductsController : ControllerBase
{
    private static readonly string[] Sizes = { "S", "M", "L", "XL", "XXL" };
    private readonly AppDbContext _db;
    private readonly IImageStorage _storage;
    public AdminProductsController(AppDbContext db, IImageStorage storage) { _db = db; _storage = storage; }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var isAdmin = User.IsInRole("Admin");

        var query = _db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term));
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                Category = p.Category.Name,
                p.Price,
                p.Mrp,
                p.CostPrice,
                p.IsActive,
                Stock = p.Variants.Sum(v => v.StockQuantity),
                Image = p.Images.Where(i => i.IsPrimary).Select(i => i.Url).FirstOrDefault()
            })
            .ToListAsync();

        // CostPrice is only copied into the response for Admin
        var items = rows.Select(r => new AdminProductListDto(
            r.Id, r.Name, r.Slug, r.Category, r.Price, r.Mrp,
            isAdmin ? r.CostPrice : null, r.IsActive, r.Stock, r.Image));

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var p = await _db.Products.AsNoTracking()
            .Include(x => x.Variants).Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == id);
        return p is null ? NotFound() : Ok(ToDetail(p));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest req)
    {
        var error = await ValidateAsync(req) ?? ValidateStock(req.Stock);
        if (error is not null) return BadRequest(new { error });

        var product = new Product
        {
            Name = req.Name.Trim(),
            Slug = await UniqueSlugAsync(req.Name),
            Description = req.Description,
            CategoryId = req.CategoryId,
            Color = req.Color,
            Fabric = req.Fabric,
            Price = req.Price,
            Mrp = req.Mrp,
            CostPrice = req.CostPrice,
            WeightKg = req.WeightKg,
            Tag = req.Tag,
            Variants = Sizes.Select(s => new ProductVariant
            {
                Size = s,
                StockQuantity = req.Stock is not null && req.Stock.TryGetValue(s, out var n) ? n : 0
            }).ToList()
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = product.Id }, ToDetail(product));
    }

    // Slug is deliberately NOT changed on update, so product URLs stay stable.
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] ProductRequest req)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        var error = await ValidateAsync(req);
        if (error is not null) return BadRequest(new { error });

        p.Name = req.Name.Trim();
        p.Description = req.Description;
        p.CategoryId = req.CategoryId;
        p.Color = req.Color;
        p.Fabric = req.Fabric;
        p.Price = req.Price;
        p.Mrp = req.Mrp;
        p.CostPrice = req.CostPrice;
        p.WeightKg = req.WeightKg;
        p.Tag = req.Tag;
        p.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:int}/stock")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetStock(int id, [FromBody] Dictionary<string, int> stock)
    {
        var error = ValidateStock(stock);
        if (error is not null) return BadRequest(new { error });

        var variants = await _db.ProductVariants.Where(v => v.ProductId == id).ToListAsync();
        if (variants.Count == 0) return NotFound();

        foreach (var (size, qty) in stock)
            variants.First(v => v.Size == size).StockQuantity = qty;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // an order changed this stock between our read and our write (xmin check failed)
            return Conflict(new { error = "Stock changed while you were editing, probably because of a new order. Reload and try again." });
        }
        return NoContent();
    }

    [HttpPatch("{id:int}/active")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetActiveRequest req)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        p.IsActive = req.IsActive;
        p.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---- helpers ----------------------------------------------------------

    private async Task<string?> ValidateAsync(ProductRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 150) return "Name is required (max 150 characters).";
        if (r.Price <= 0) return "Price must be greater than 0.";
        if (r.Mrp < r.Price) return "MRP must be at least the selling price.";
        if (r.CostPrice < 0) return "Cost price cannot be negative.";
        if (r.WeightKg <= 0 || r.WeightKg > 10) return "Weight must be between 0 and 10 kg.";
        if (!await _db.Categories.AnyAsync(c => c.Id == r.CategoryId && c.IsActive)) return "Category not found.";
        return null;
    }

    private static string? ValidateStock(Dictionary<string, int>? stock)
    {
        if (stock is null) return null;
        foreach (var (size, qty) in stock)
        {
            if (!Sizes.Contains(size)) return $"Unknown size '{size}'. Use S, M, L, XL or XXL.";
            if (qty < 0) return "Stock cannot be negative.";
        }
        return null;
    }

    private async Task<string> UniqueSlugAsync(string name)
    {
        var baseSlug = Regex.Replace(name.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (baseSlug.Length == 0) baseSlug = "product";
        if (baseSlug.Length > 150) baseSlug = baseSlug[..150];

        var slug = baseSlug;
        var n = 2;
        while (await _db.Products.AnyAsync(p => p.Slug == slug))
            slug = $"{baseSlug}-{n++}";
        return slug;
    }

    private AdminProductDetailDto ToDetail(Product p) => new(
        p.Id, p.Name, p.Slug, p.Description, p.CategoryId, p.Color, p.Fabric,
        p.Price, p.Mrp, User.IsInRole("Admin") ? p.CostPrice : null, p.WeightKg, p.Tag, p.IsActive,
        p.Variants.OrderBy(v => Array.IndexOf(Sizes, v.Size))
            .Select(v => new AdminVariantDto(v.Id, v.Size, v.StockQuantity)).ToList(),
        p.Images.OrderBy(i => i.SortOrder)
            .Select(i => new AdminImageDto(i.Id, i.Url, i.AltText, i.SortOrder, i.IsPrimary)).ToList());

    private const int MaxImagesPerProduct = 8;
    private const long MaxImageBytes = 5 * 1024 * 1024;

    [HttpPost("{id:int}/images")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(45_000_000)]
    public async Task<ActionResult<List<ProductImageDto>>> UploadImages(
        int id, [FromForm] List<IFormFile> files, CancellationToken ct)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == id, ct)) return NotFound();
        if (files.Count == 0) return BadRequest("Send at least one file in the 'files' field.");

        var existing = await _db.ProductImages.Where(i => i.ProductId == id).ToListAsync(ct);
        if (existing.Count + files.Count > MaxImagesPerProduct)
            return BadRequest($"A product can have at most {MaxImagesPerProduct} images (it has {existing.Count}).");

        // 1) Validate EVERY file first, so we never save half a batch
        var checkedFiles = new List<(IFormFile File, string Ext)>();
        foreach (var f in files)
        {
            if (f.Length == 0 || f.Length > MaxImageBytes)
                return BadRequest($"'{f.FileName}': file must be between 1 byte and 5 MB.");

            await using var s = f.OpenReadStream();
            var header = new byte[12];
            var read = await s.ReadAsync(header.AsMemory(0, 12), ct);
            var ext = DetectImageExtension(header, read);
            if (ext is null)
                return BadRequest($"'{f.FileName}': only real JPEG, PNG or WebP images are allowed.");
            checkedFiles.Add((f, ext));
        }

        // 2) Save to storage, then to the DB; if the DB fails, remove the files again
        var saved = new List<StoredImage>();
        try
        {
            var nextSort = existing.Count == 0 ? 0 : existing.Max(i => i.SortOrder) + 1;
            var hasPrimary = existing.Any(i => i.IsPrimary);
            var created = new List<ProductImage>();

            foreach (var (file, ext) in checkedFiles)
            {
                await using var stream = file.OpenReadStream();
                var stored = await _storage.SaveAsync(stream, ext, ct);
                saved.Add(stored);

                var image = new ProductImage
                {
                    ProductId = id,
                    Url = stored.Url,
                    StorageKey = stored.StorageKey,
                    SortOrder = nextSort++,
                    IsPrimary = !hasPrimary          // first image ever becomes the cover
                };
                hasPrimary = true;
                created.Add(image);
                _db.ProductImages.Add(image);
            }

            await _db.SaveChangesAsync(ct);
            return Ok(created.Select(ToImageDto).ToList());
        }
        catch
        {
            foreach (var s in saved) await _storage.DeleteAsync(s.StorageKey, CancellationToken.None);
            throw;
        }
    }

    // Checks the file's real first bytes, not its name or the Content-Type the client claims.
    private static string? DetectImageExtension(byte[] h, int len)
    {
        if (len >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (len >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return ".png";
        if (len >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F'
                      && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return ".webp";
        return null;
    }
    private ProductImageDto ToImageDto(ProductImage i) =>
    new(i.Id, i.StorageKey != null ? _storage.GetUrl(i.StorageKey) : i.Url,
        i.AltText, i.SortOrder, i.IsPrimary);

    [HttpPut("{id:int}/images/order")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<ProductImageDto>>> ReorderImages(
        int id, ReorderImagesRequest req, CancellationToken ct)
    {
        var images = await _db.ProductImages.Where(i => i.ProductId == id).ToListAsync(ct);
        if (images.Count == 0) return NotFound();

        var ids = req.ImageIds ?? new List<int>();
        if (ids.Count != images.Count
            || ids.Distinct().Count() != ids.Count
            || !ids.All(x => images.Any(i => i.Id == x)))
            return BadRequest("imageIds must list every image of this product exactly once.");

        for (var pos = 0; pos < ids.Count; pos++)
            images.First(i => i.Id == ids[pos]).SortOrder = pos;

        await _db.SaveChangesAsync(ct);
        return Ok(images.OrderBy(i => i.SortOrder).Select(ToImageDto).ToList());
    }

    [HttpPatch("{id:int}/images/{imageId:int}/primary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetPrimaryImage(int id, int imageId, CancellationToken ct)
    {
        var images = await _db.ProductImages.Where(i => i.ProductId == id).ToListAsync(ct);
        if (!images.Any(i => i.Id == imageId)) return NotFound();

        foreach (var i in images) i.IsPrimary = i.Id == imageId;   // exactly one primary
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}/images/{imageId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteImage(int id, int imageId, CancellationToken ct)
    {
        var images = await _db.ProductImages
            .Where(i => i.ProductId == id).OrderBy(i => i.SortOrder).ToListAsync(ct);
        var image = images.FirstOrDefault(i => i.Id == imageId);
        if (image is null) return NotFound();

        _db.ProductImages.Remove(image);
        if (image.IsPrimary)
        {
            var next = images.FirstOrDefault(i => i.Id != imageId);   // promote the next image
            if (next != null) next.IsPrimary = true;
        }

        await _db.SaveChangesAsync(ct);                     // DB first...
        if (image.StorageKey != null)
            await _storage.DeleteAsync(image.StorageKey, ct); // ...then the file
        return NoContent();
    }
}