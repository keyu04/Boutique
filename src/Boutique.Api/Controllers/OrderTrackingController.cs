using System.Text.RegularExpressions;
using Boutique.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Boutique.Api.Controllers;

public record TrackOrderRequest(string? OrderNumber, string? Phone);
public record TrackItem(string ProductName, string Size, int Quantity);
public record TrackStep(string Status, DateTime At);
public record TrackOrderResponse(
    string OrderNumber, string Status, DateTime PlacedAt,
    string PaymentMethod, string PaymentStatus, decimal Total,
    string? CourierName, string? TrackingNumber,
    IReadOnlyList<TrackItem> Items, IReadOnlyList<TrackStep> Timeline);

[ApiController]
[Route("api/orders/track")]
public class OrderTrackingController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrderTrackingController(AppDbContext db) => _db = db;

    [HttpPost]
    [EnableRateLimiting("track")]
    public async Task<IActionResult> Track([FromBody] TrackOrderRequest req)
    {
        // One answer for "no such order" and "wrong phone", so nobody can probe which numbers exist.
        var notFound = NotFound(new { error = "We couldn't find an order with those details." });

        var number = (req.OrderNumber ?? "").Trim().ToUpperInvariant();
        var phone = Regex.Replace(req.Phone ?? "", @"\D", "");
        if (phone.Length > 10) phone = phone[^10..];          // accept +91 / 0 prefixes
        if (number.Length is 0 or > 40 || phone.Length != 10) return notFound;

        var o = await _db.Orders.AsNoTracking()
            .Where(x => x.OrderNumber == number && x.Phone == phone)
            .Select(x => new
            {
                x.OrderNumber, x.Status, x.CreatedAt, x.PaymentMethod, x.PaymentStatus, x.Total,
                x.CourierName, x.TrackingNumber,
                Items = x.Items.Select(i => new { i.ProductNameSnapshot, i.Size, i.Quantity }).ToList(),
                History = x.StatusHistory.OrderBy(h => h.ChangedAt)
                    .Select(h => new { h.ToStatus, h.ChangedAt }).ToList(),
            })
            .FirstOrDefaultAsync();

        if (o is null) return notFound;

        return Ok(new TrackOrderResponse(
            o.OrderNumber, o.Status.ToString(), o.CreatedAt,
            o.PaymentMethod.ToString(), o.PaymentStatus.ToString(), o.Total,
            o.CourierName, o.TrackingNumber,
            o.Items.Select(i => new TrackItem(i.ProductNameSnapshot, i.Size, i.Quantity)).ToList(),
            o.History.Select(h => new TrackStep(h.ToStatus.ToString(), h.ChangedAt)).ToList()));
    }
}