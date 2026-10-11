namespace Boutique.Domain;

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public int ProductId { get; set; }
    public int VariantId { get; set; }

    // snapshots — later edits to the product must never change past orders
    public string ProductNameSnapshot { get; set; } = default!;
    public string Size { get; set; } = default!;
    public decimal UnitPriceSnapshot { get; set; }
    public decimal UnitCostSnapshot { get; set; }

    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}