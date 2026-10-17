using Boutique.Application.Interfaces;
using Boutique.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Boutique.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private static readonly string[] SizeOrder = { "XS", "S", "M", "L", "XL", "XXL" };

    private readonly AppDbContext _db;
    private readonly IImageStorage _storage;

    public ProductsController(AppDbContext db, IImageStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    // uploaded image -> URL built from config; seeded image -> stored URL
    private string ImageUrl(string? key, string url) => key != null ? _storage.GetUrl(key) : url;

    private static int SizeRank(string size)
    {
        var i = Array.IndexOf(SizeOrder, size);
        return i < 0 ? int.MaxValue : i;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? q, [FromQuery] string? category, [FromQuery] decimal? maxPrice,
        [FromQuery] string? sort, [FromQuery] int page = 1, [FromQuery] int pageSize = 12,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 48);

        var query = _db.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term)
                                  || p.Color.ToLower().Contains(term)
                                  || p.Fabric.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category.Slug == category);
        if (maxPrice.HasValue)
            query = query.Where(p => p.Price <= maxPrice.Value);

        query = sort switch
        {
            "price_asc"  => query.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "price_desc" => query.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            "newest"     => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
            _            => query.OrderBy(p => p.Id)
        };

        var total = await query.CountAsync(ct);

        var rows = await query
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                p.Id, p.Name, p.Slug, p.Price, p.Mrp, p.Color, p.Fabric, p.Tag,
                ImageUrl = p.Images.Where(i => i.IsPrimary).Select(i => i.Url).FirstOrDefault(),
                ImageKey = p.Images.Where(i => i.IsPrimary).Select(i => i.StorageKey).FirstOrDefault(),
                Sizes = p.Variants.Select(v => new { v.Id, v.Size, v.StockQuantity }).ToList()
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new
        {
            r.Id, r.Name, r.Slug, r.Price, r.Mrp, r.Color, r.Fabric, r.Tag,
            Image = r.ImageKey != null ? _storage.GetUrl(r.ImageKey) : r.ImageUrl,
            Sizes = r.Sizes.OrderBy(s => SizeRank(s.Size))
                           .Select(s => new { VariantId = s.Id, s.Size, s.StockQuantity })
        });

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        var p = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive && x.Slug == slug)
            .Select(x => new
            {
                x.Id, x.Name, x.Slug, x.Description, x.Price, x.Mrp, x.Color, x.Fabric, x.Tag,
                Category = x.Category.Name,
                CategorySlug = x.Category.Slug,
                Images = x.Images.OrderBy(i => i.SortOrder)
                    .Select(i => new { i.Id, i.Url, i.StorageKey, i.AltText, i.IsPrimary }).ToList(),
                Sizes = x.Variants.Select(v => new { v.Id, v.Size, v.StockQuantity }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (p is null) return NotFound();

        return Ok(new
        {
            p.Id, p.Name, p.Slug, p.Description, p.Price, p.Mrp, p.Color, p.Fabric, p.Tag,
            p.Category, p.CategorySlug,
            Images = p.Images.Select(i => new { i.Id, Url = ImageUrl(i.StorageKey, i.Url), i.AltText, i.IsPrimary }),
            Sizes = p.Sizes.OrderBy(s => SizeRank(s.Size))
                           .Select(s => new { VariantId = s.Id, s.Size, s.StockQuantity })
        });
    }
}