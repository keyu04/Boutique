using Boutique.Application.Dtos;
using Boutique.Application.Interfaces;
using Boutique.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Boutique.Api.Controllers;

[ApiController]
[Route("api/shipping")]
public class ShippingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IShippingService _shipping;

    public ShippingController(AppDbContext db, IShippingService shipping)
    {
        _db = db;
        _shipping = shipping;
    }

    [HttpPost("estimate")]
    public async Task<IActionResult> Estimate([FromBody] ShippingEstimateRequest req)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(req.Pincode, @"^\d{6}$"))
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
            .ToListAsync();

        if (variants.Count != variantIds.Distinct().Count())
            return BadRequest(new { error = "One or more items are no longer available." });

        decimal totalWeight = 0;
        foreach (var item in req.Items)
        {
            var variant = variants.First(v => v.Id == item.VariantId);
            totalWeight += variant.Product.WeightKg * item.Quantity;
        }

        var (charge, eta, chargeableWeight) = _shipping.Estimate(totalWeight, req.Pincode, req.PaymentMethod);
        return Ok(new ShippingEstimateResponse(charge, eta, chargeableWeight));
    }
}