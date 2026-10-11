namespace Boutique.Domain;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public string? Color { get; set; }
    public string? Fabric { get; set; }

    public decimal Price { get; set; }
    public decimal Mrp { get; set; }
    public decimal CostPrice { get; set; } // admin only — never expose in public DTOs

    public decimal WeightKg { get; set; }
    public string? Tag { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}