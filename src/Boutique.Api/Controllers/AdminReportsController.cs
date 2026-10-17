using Boutique.Application.Dtos;
using Boutique.Domain;
using Boutique.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Boutique.Api.Controllers;

[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = "Admin")]
public class AdminReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminReportsController(AppDbContext db) => _db = db;

    // Orders that count as real sales. Pending (unpaid) and Cancelled are excluded.
    private static readonly OrderStatus[] CountedStatuses =
    {
        OrderStatus.Confirmed, OrderStatus.Packed, OrderStatus.Shipped, OrderStatus.Delivered
    };

    [HttpGet("summary")]
    public async Task<ActionResult<ReportSummaryDto>> Summary(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = from ?? toDate.AddDays(-29);
        if (fromDate > toDate) return BadRequest(new { error = "'from' must not be after 'to'." });
        if (toDate.DayNumber - fromDate.DayNumber > 366)
            return BadRequest(new { error = "The date range can be at most one year." });

        // 'to' is inclusive: query up to the start of the next day (UTC)
        var start = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var inRange = _db.Orders.AsNoTracking().Where(o => o.CreatedAt >= start && o.CreatedAt < end);

        // Count of every status (zero when none), so the dashboard always has all keys
        var byStatus = await inRange
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var ordersByStatus = Enum.GetValues<OrderStatus>().ToDictionary(
            s => s.ToString(),
            s => byStatus.FirstOrDefault(x => x.Status == s)?.Count ?? 0);

        // Money: only orders that count as sales
        var counted = inRange.Where(o => CountedStatuses.Contains(o.Status));
        var orderCount = await counted.CountAsync(ct);
        var shipping = await counted.SumAsync(o => (decimal?)o.ShippingCharge, ct) ?? 0m;

        var lines = from i in _db.OrderItems.AsNoTracking()
                    join o in counted on i.OrderId equals o.Id
                    select i;
        var itemRevenue = await lines.SumAsync(i => (decimal?)i.LineTotal, ct) ?? 0m;
        var cost = await lines.SumAsync(i => (decimal?)(i.UnitCostSnapshot * i.Quantity), ct) ?? 0m;

        var margin = itemRevenue - cost;
        var marginPercent = itemRevenue > 0 ? Math.Round(margin / itemRevenue * 100m, 1) : 0m;

        return Ok(new ReportSummaryDto(fromDate, toDate, orderCount, itemRevenue, shipping,
    Math.Round(cost, 2), Math.Round(margin, 2), marginPercent, ordersByStatus));
    }
}