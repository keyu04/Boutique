using Boutique.Application.Dtos;
using Boutique.Application.Interfaces;
using Boutique.Domain;
using Boutique.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Boutique.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IShippingService _shipping;
    private readonly IOrderNumberGenerator _orderNumbers;

    public OrdersController(AppDbContext db, IShippingService shipping, IOrderNumberGenerator orderNumbers)
    {
        _db = db;
        _shipping = shipping;
        _orderNumbers = orderNumbers;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrderRequest req)
    {
        // 1. Basic validation
        if (req.Items is null || req.Items.Count == 0)
            return BadRequest(new { error = "Your bag is empty." });
        if (string.IsNullOrWhiteSpace(req.CustomerName))
            return BadRequest(new { error = "Name is required." });
        if (!System.Text.RegularExpressions.Regex.IsMatch(req.Phone ?? "", @"^\d{10}$"))
            return BadRequest(new { error = "Enter a valid 10-digit phone number." });
        if (!System.Text.RegularExpressions.Regex.IsMatch(req.Pincode ?? "", @"^\d{6}$"))
            return BadRequest(new { error = "Enter a valid 6-digit pincode." });
        if (string.IsNullOrWhiteSpace(req.AddressLine) || string.IsNullOrWhiteSpace(req.City) || string.IsNullOrWhiteSpace(req.State))
            return BadRequest(new { error = "Full address, city and state are required." });
        if (!Enum.TryParse<PaymentMethod>(req.PaymentMethod, true, out var paymentMethod))
            return BadRequest(new { error = "Invalid payment method." });

        // merge duplicate variant lines
        var mergedItems = req.Items
            .GroupBy(i => i.VariantId)
            .Select(g => new { VariantId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        const int maxRetries = 1;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var variantIds = mergedItems.Select(i => i.VariantId).ToList();
                var variants = await _db.ProductVariants
                    .Include(v => v.Product)
                    .Where(v => variantIds.Contains(v.Id))
                    .ToListAsync();

                if (variants.Count != variantIds.Count)
                    return BadRequest(new { error = "One or more items are no longer available." });

                decimal subtotal = 0;
                decimal totalWeight = 0;
                var orderItems = new List<OrderItem>();

                foreach (var item in mergedItems)
                {
                    var variant = variants.First(v => v.Id == item.VariantId);
                    if (variant.StockQuantity < item.Quantity)
                        return Conflict(new { error = $"Only {variant.StockQuantity} left for {variant.Product.Name} (size {variant.Size})." });

                    variant.StockQuantity -= item.Quantity; // deduct now, inside the transaction
                    totalWeight += variant.Product.WeightKg * item.Quantity;
                    subtotal += variant.Product.Price * item.Quantity;

                    orderItems.Add(new OrderItem
                    {
                        ProductId = variant.ProductId,
                        VariantId = variant.Id,
                        ProductNameSnapshot = variant.Product.Name,
                        Size = variant.Size,
                        UnitPriceSnapshot = variant.Product.Price,
                        UnitCostSnapshot = variant.Product.CostPrice,
                        Quantity = item.Quantity,
                        LineTotal = variant.Product.Price * item.Quantity
                    });
                }

                var (shippingCharge, _, _) = _shipping.Estimate(totalWeight, req.Pincode, req.PaymentMethod);

                var order = new Order
                {
                    OrderNumber = _orderNumbers.Generate(),
                    Status = OrderStatus.Pending,
                    PaymentMethod = paymentMethod,
                    PaymentStatus = paymentMethod == PaymentMethod.Cod ? PaymentStatus.CodDue : PaymentStatus.Pending,
                    CustomerName = req.CustomerName,
                    Phone = req.Phone,
                    AddressLine = req.AddressLine,
                    City = req.City,
                    State = req.State,
                    Pincode = req.Pincode,
                    Subtotal = subtotal,
                    ShippingCharge = shippingCharge,
                    Total = subtotal + shippingCharge,
                    Items = orderItems,
                    StatusHistory = new List<OrderStatusHistory>
                    {
                        new() { FromStatus = OrderStatus.Pending, ToStatus = OrderStatus.Pending, ChangedBy = "system", Note = "Order created" }
                    }
                };

                _db.Orders.Add(order);
                await _db.SaveChangesAsync();   // <-- concurrency conflict on stock would throw here
                await tx.CommitAsync();

                return Ok(new OrderResponse(
                    order.OrderNumber, order.Status.ToString(),
                    order.Subtotal, order.ShippingCharge, order.Total,
                    order.PaymentMethod.ToString(), order.PaymentStatus.ToString()));
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync();
                if (attempt == maxRetries)
                    return Conflict(new { error = "Stock changed while placing your order. Please try again." });
                // loop retries once, re-reading fresh stock
            }
        }

        return Conflict(new { error = "Could not place order. Please try again." });
    }
}