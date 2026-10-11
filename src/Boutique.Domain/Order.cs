namespace Boutique.Domain;

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string? PaymentReference { get; set; }

    public string CustomerName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string AddressLine { get; set; } = default!;
    public string City { get; set; } = default!;
    public string State { get; set; } = default!;
    public string Pincode { get; set; } = default!;

    public decimal Subtotal { get; set; }
    public decimal ShippingCharge { get; set; }
    public decimal Total { get; set; }

    public string? CourierName { get; set; }
    public string? TrackingNumber { get; set; }

    public uint Version  { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
}