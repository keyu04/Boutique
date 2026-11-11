using Boutique.Application.Dtos;
using Boutique.Application.Interfaces;
using Boutique.Domain;
using Boutique.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Boutique.Api.Controllers;

[ApiController]
[Route("api/shipping")]
public class ShippingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IShippingQuoteService _quotes;

    public ShippingController(AppDbContext db, IShippingQuoteService quotes)
    {
        _db = db;
        _quotes = quotes;
    }

    [HttpPost("estimate")]
    [EnableRateLimiting("estimate")]
    public async Task<IActionResult> Estimate([FromBody] ShippingEstimateRequest req, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(req.Pincode ?? "", @"^\d{6}$"))
            return BadRequest(new { error = "Enter a valid six-digit pincode." });

        if (req.Items is null || req.Items.Count == 0)
            return BadRequest(new { error = "No items to estimate." });

        var qtyError = CheckoutRules.ValidateQuantities(req.Items.Select(i => i.Quantity).ToList());
        if (qtyError != null) return BadRequest(new { error = qtyError });

        if (!CheckoutRules.TryParsePaymentMethod(req.PaymentMethod, out var paymentMethod))
            return BadRequest(new { error = "PaymentMethod must be 'Prepaid' or 'Cod'." });

        var variantIds = req.Items.Select(i => i.VariantId).ToList();
        var variants = await _db.ProductVariants
            .Include(v => v.Product)
            .Where(v => variantIds.Contains(v.Id))
            .ToListAsync(ct);

        if (variants.Count != variantIds.Distinct().Count())
            return BadRequest(new { error = "One or more items are no longer available." });

        decimal totalWeight = 0, subtotal = 0, itemCost = 0;
        foreach (var item in req.Items)
        {
            var variant = variants.First(v => v.Id == item.VariantId);
            totalWeight += variant.Product.WeightKg * item.Quantity;
            subtotal += variant.Product.Price * item.Quantity;
            itemCost += variant.Product.CostPrice * item.Quantity;
        }

        var quote = await _quotes.QuoteAsync(
            new ShippingQuoteRequest(req.Pincode!, paymentMethod == PaymentMethod.Cod, totalWeight, subtotal, itemCost), ct);

        if (!quote.Deliverable) return BadRequest(new { error = quote.Message });

        return Ok(new ShippingEstimateResponse(
            quote.Charge, quote.Eta, quote.ChargeableWeightKg,
            quote.StandardCharge, quote.Saving, quote.AmountToFreeDelivery,
            quote.FreeDeliveryAtSubtotal, quote.PrepaidSaving, quote.DeliveryBy?.ToString("O")));
    }
}