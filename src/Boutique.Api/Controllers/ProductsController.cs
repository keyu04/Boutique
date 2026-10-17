using Boutique.Application.Interfaces;
using Boutique.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Boutique.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IImageStorage _storage;

    public ProductsController(AppDbContext db, IImageStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        // 1) Query the database: only raw columns, no C# method calls inside the query
        var rows = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.Price,
                p.Mrp,
                p.Color,
                p.Fabric,
                p.Tag,
                ImageUrl = p.Images.Where(i => i.IsPrimary).Select(i => i.Url).FirstOrDefault(),
                ImageKey = p.Images.Where(i => i.IsPrimary).Select(i => i.StorageKey).FirstOrDefault(),
                Sizes = p.Variants.Select(v => new { v.Size, v.StockQuantity }).ToList()
            })
            .ToListAsync();

        // 2) Build the final URL in memory (uploaded image -> from config, seeded image -> stored URL)
        var products = rows.Select(r => new
        {
            r.Id,
            r.Name,
            r.Slug,
            r.Price,
            r.Mrp,
            r.Color,
            r.Fabric,
            r.Tag,
            Image = r.ImageKey != null ? _storage.GetUrl(r.ImageKey) : r.ImageUrl,
            r.Sizes
        });

        return Ok(products);
    }
}