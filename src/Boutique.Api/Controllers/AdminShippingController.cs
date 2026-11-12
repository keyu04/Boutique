// check price directly from courier api, without using our own rates
using Boutique.Application.Interfaces;
using Boutique.Infrastructure.Shipping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Boutique.Api.Controllers;

[ApiController]
[Route("api/admin/shipping")]
[Authorize(Roles = "Admin")]
public class AdminShippingController : ControllerBase
{
    private readonly ICourierRateProvider _rates;
    private readonly ShiprocketOptions _opt;

    public AdminShippingController(ICourierRateProvider rates, IOptions<ShiprocketOptions> opt)
    {
        _rates = rates;
        _opt = opt.Value;
    }

    [HttpGet("quotes")]
    public async Task<IActionResult> Quotes(
        [FromQuery] string pincode,
        [FromQuery] decimal weightKg = 1,
        [FromQuery] bool cod = false,
        CancellationToken ct = default)
    {
        var quotes = await _rates.GetQuotesAsync(_opt.PickupPincode, pincode, weightKg, cod, ct);
        return quotes is null
            ? StatusCode(502, new { error = "Courier rates are unavailable right now." })
            : Ok(quotes);
    }
}