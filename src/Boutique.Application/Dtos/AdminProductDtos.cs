namespace Boutique.Application.Dtos;

public class ProductRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public string? Color { get; set; }
    public string? Fabric { get; set; }
    public decimal Price { get; set; }
    public decimal Mrp { get; set; }
    public decimal CostPrice { get; set; }
    public decimal WeightKg { get; set; }
    public string? Tag { get; set; }
}

public class CreateProductRequest : ProductRequest
{
    public Dictionary<string, int>? Stock { get; set; } // e.g. { "S": 4, "M": 6 }; missing sizes start at 0
}

public record SetActiveRequest(bool IsActive);

public record AdminProductListDto(
    int Id, string Name, string Slug, string CategoryName,
    decimal Price, decimal Mrp, decimal? CostPrice,   // CostPrice is null for Staff
    bool IsActive, int TotalStock, string? Image);

public record AdminVariantDto(int Id, string Size, int StockQuantity);
public record AdminImageDto(int Id, string Url, string? AltText, int SortOrder, bool IsPrimary);

public record AdminProductDetailDto(
    int Id, string Name, string Slug, string? Description, int CategoryId,
    string? Color, string? Fabric, decimal Price, decimal Mrp, decimal? CostPrice,
    decimal WeightKg, string? Tag, bool IsActive,
    List<AdminVariantDto> Variants, List<AdminImageDto> Images);
public record ProductImageDto(int Id, string Url, string? AltText, int SortOrder, bool IsPrimary);