using Boutique.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Boutique.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _db.Products
            .Where(p => p.IsActive)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Select(p => new
            {
                p.Id, p.Name, p.Slug, p.Price, p.Mrp, p.Color, p.Fabric, p.Tag,
                Image = p.Images.Where(i => i.IsPrimary).Select(i => i.Url).FirstOrDefault(),
                Sizes = p.Variants.Select(v => new { v.Size, v.StockQuantity })
            })
            .ToListAsync();

        return Ok(products);
    }
}