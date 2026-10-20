using Boutique.Application.Interfaces;
using Boutique.Domain;
using Boutique.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Boutique.Api.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "Admin,Staff")]
public class AdminOrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IImageStorage _storage;

    public AdminOrdersController(AppDbContext db, IImageStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    // GET /api/admin/orders?status=Pending&from=2026-10-01&to=2026-10-31&q=VEL&page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Orders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryParseStatus(status, out var s))
                return Fail(400, $"Unknown status '{status}'.");
            query = query.Where(o => o.Status == s);
        }
        if (from.HasValue)
        {
            // Npgsql only accepts UTC DateTimes for timestamptz columns
            var start = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc);
            query = query.Where(o => o.CreatedAt >= start);
        }
        if (to.HasValue)
        {
            var end = DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc); // 'to' is inclusive
            query = query.Where(o => o.CreatedAt < end);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(o => o.OrderNumber.ToLower().Contains(term)
                                  || o.CustomerName.ToLower().Contains(term)
                                  || o.Phone.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.CreatedAt, o.Status, o.PaymentMethod, o.PaymentStatus,
                o.CustomerName, o.Phone, o.City, o.Total,
                ItemCount = o.Items.Sum(i => i.Quantity)
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new AdminOrderListItemDto(
            r.Id, r.OrderNumber, r.CreatedAt, r.Status.ToString(), r.PaymentMethod.ToString(),
            r.PaymentStatus.ToString(), r.CustomerName, r.Phone, r.City, r.Total, r.ItemCount)).ToList();

        return Ok(new AdminOrderListResponse(items, page, pageSize, total));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var dto = await LoadDetailAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, ChangeStatusRequest req, CancellationToken ct)
    {
        if (!TryParseStatus(req.Status, out var target))
            return Fail(400, $"Unknown status '{req.Status}'.");
        if (req.Note is { Length: > 300 })
            return Fail(400, "Note can be at most 300 characters.");

        var changedBy = User.Identity?.Name ?? "unknown";

        // Two attempts: if someone else changed the same order/stock at the same moment, reload and retry once.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);
            if (order is null) return NotFound();

            if (!OrderStatusRules.CanTransition(order.Status, target))
                return Fail(409, $"An order that is {order.Status} cannot move to {target}.");

            if (target == OrderStatus.Cancelled)
            {
                // Stock was deducted when the order was created, so give it back
                var variantIds = order.Items.Select(i => i.VariantId).Distinct().ToList();
                var variants = await _db.ProductVariants.Where(v => variantIds.Contains(v.Id)).ToListAsync(ct);
                foreach (var item in order.Items)
                {
                    var variant = variants.FirstOrDefault(v => v.Id == item.VariantId);
                    if (variant != null) variant.StockQuantity += item.Quantity;
                }
            }

            if (target == OrderStatus.Delivered
                && order.PaymentMethod == PaymentMethod.Cod
                && order.PaymentStatus == PaymentStatus.CodDue)
                order.PaymentStatus = PaymentStatus.Paid;   // cash collected on delivery

            order.StatusHistory.Add(new OrderStatusHistory
            {
                FromStatus = order.Status,
                ToStatus = target,
                ChangedBy = changedBy,
                Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim()
            });
            order.Status = target;

            try
            {
                await _db.SaveChangesAsync(ct);   // one SaveChanges = one transaction: status + history + stock
                return Ok(await LoadDetailAsync(id, ct));
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.ChangeTracker.Clear();        // throw away stale data, loop reloads fresh rows
            }
        }

        return Fail(409, "This order was changed by someone else at the same time. Reload and try again.");
    }

    [HttpPatch("{id:int}/tracking")]
    public async Task<IActionResult> SetTracking(int id, SetTrackingRequest req, CancellationToken ct)
    {
        var courier = req.CourierName?.Trim();
        var tracking = req.TrackingNumber?.Trim();
        if (string.IsNullOrEmpty(courier) || courier.Length > 60)
            return Fail(400, "Courier name is required (max 60 characters).");
        if (string.IsNullOrEmpty(tracking) || tracking.Length > 60)
            return Fail(400, "Tracking number is required (max 60 characters).");

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return NotFound();

        if (order.Status is not (OrderStatus.Packed or OrderStatus.Shipped or OrderStatus.Delivered))
            return Fail(409, $"Tracking can be set only once the order is Packed (it is {order.Status}).");

        order.CourierName = courier;
        order.TrackingNumber = tracking;

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return Fail(409, "This order was changed by someone else at the same time. Reload and try again.");
        }
        return Ok(await LoadDetailAsync(id, ct));
    }

    // ---- helpers ------------------------------------------------------------

    private ObjectResult Fail(int status, string detail) => Problem(detail: detail, statusCode: status);

    // Accept only the names ("Confirmed"), never numbers like "3"
    private static bool TryParseStatus(string? text, out OrderStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var name = Enum.GetNames<OrderStatus>()
            .FirstOrDefault(n => n.Equals(text.Trim(), StringComparison.OrdinalIgnoreCase));
        if (name is null) return false;
        status = Enum.Parse<OrderStatus>(name);
        return true;
    }

    private async Task<AdminOrderDetailDto?> LoadDetailAsync(int id, CancellationToken ct)
    {
        var o = await _db.Orders.AsNoTracking().AsSplitQuery()
            .Include(x => x.Items).Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (o is null) return null;

        var isAdmin = User.IsInRole("Admin");   // Staff must never see cost
        var productIds = o.Items.Select(i => i.ProductId).Distinct().ToArray();
        var imageRows = await _db.ProductImages.AsNoTracking()
            .Where(i => productIds.Contains(i.ProductId))
            .Select(i => new { i.ProductId, i.Url, i.StorageKey, i.IsPrimary, i.SortOrder })
            .ToListAsync(ct);
        var imagesByProductId = imageRows
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.SortOrder).First());

        return new AdminOrderDetailDto(
            o.Id, o.OrderNumber, o.CreatedAt, o.Status.ToString(), o.PaymentMethod.ToString(),
            o.PaymentStatus.ToString(), o.PaymentReference,
            o.CustomerName, o.Phone, o.AddressLine, o.City, o.State, o.Pincode,
            o.Subtotal, o.ShippingCharge, o.Total, o.CourierName, o.TrackingNumber,
            OrderStatusRules.NextStatuses(o.Status).Select(s => s.ToString()).ToList(),
            o.Items.OrderBy(i => i.Id).Select(i => new AdminOrderItemDto(
                i.ProductId, i.VariantId, i.ProductNameSnapshot, i.Size, i.UnitPriceSnapshot,
                isAdmin ? i.UnitCostSnapshot : null, i.Quantity, i.LineTotal,
                imagesByProductId.TryGetValue(i.ProductId, out var image)
                    ? image.StorageKey != null ? _storage.GetUrl(image.StorageKey) : image.Url
                    : null)).ToList(),
            o.StatusHistory.OrderBy(h => h.ChangedAt).ThenBy(h => h.Id).Select(h => new AdminOrderHistoryDto(
                h.FromStatus.ToString(), h.ToStatus.ToString(), h.ChangedBy, h.ChangedAt, h.Note)).ToList());
    }
}