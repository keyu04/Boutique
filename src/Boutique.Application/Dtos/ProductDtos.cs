namespace Boutique.Application.Dtos;

public record ProductListDto(
    int Id, string Name, string Slug, decimal Price,
    decimal Mrp, string? Color, string? Fabric,
    string? Tag, string? Image,
    List<SizeDto> Sizes
    );

public record SizeDto(string Size, int StockQuantity);

public record ProductDetailDto(
    int Id, string Name, string Slug, string? Description,
    string CategoryName, decimal Price, decimal Mrp,
    string? Color, string? Fabric, decimal WeightKg, string? Tag,
    List<string> Images, List<SizeDto> Sizes
    );