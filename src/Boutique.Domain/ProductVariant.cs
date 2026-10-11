namespace Boutique.Domain;

public class ProductVariant
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public string Size { get; set; } = default!; // S, M, L, XL, XXL
    public int StockQuantity { get; set; }

    // EF Core concurrency token — prevents two customers overselling the last item
    public uint Version  { get; set; }
}