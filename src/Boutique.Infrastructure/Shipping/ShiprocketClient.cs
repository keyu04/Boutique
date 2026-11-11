using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Boutique.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Boutique.Infrastructure.Shipping;

public class ShiprocketClient : ICourierRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ShiprocketOptions _options;
    private readonly ILogger<ShiprocketClient> _logger;
    private static readonly SemaphoreSlim _tokenLock = new(1, 1);
    private static string? _token;

    public ShiprocketClient(
        HttpClient httpClient,
        IOptions<ShiprocketOptions> options,
        ILogger<ShiprocketClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CourierQuote>?> GetQuotesAsync(
        string pickupPincode,
        string deliveryPincode,
        decimal weightKg,
        bool cod,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Email)
            || string.IsNullOrWhiteSpace(_options.Password)
            || string.IsNullOrWhiteSpace(pickupPincode))
        {
            _logger.LogWarning("Shiprocket credentials or pickup pincode are not configured; using fallback shipping rates.");
            return null;
        }

        try
        {
            var query = $"pickup_postcode={Uri.EscapeDataString(pickupPincode)}"
                + $"&delivery_postcode={Uri.EscapeDataString(deliveryPincode)}"
                + $"&weight={weightKg.ToString(CultureInfo.InvariantCulture)}"
                + $"&cod={(cod ? "1" : "0")}";

            for (var attempt = 0; attempt < 2; attempt++)
            {
                var token = await GetTokenAsync(ct);
                if (token is null) return null;

                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{_options.BaseUrl.TrimEnd('/')}/v1/external/courier/serviceability/?{query}");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                using var response = await _httpClient.SendAsync(request, ct);
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
                {
                    _logger.LogWarning("Shiprocket answered {StatusCode} for {Pincode}; treating as not serviceable.", response.StatusCode, deliveryPincode);
                    return Array.Empty<CourierQuote>();
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Shiprocket serviceability request failed with status {StatusCode}; using fallback shipping rates.",
                        response.StatusCode);
                    return null;
                }

                using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                return ParseQuotes(document.RootElement);
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Shiprocket request failed; using fallback shipping rates.");
            return null;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Shiprocket request timed out; using fallback shipping rates.");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Shiprocket returned an invalid response; using fallback shipping rates.");
            return null;
        }
    }

    private async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null) return _token;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_token is not null) return _token;

            var url = $"{_options.BaseUrl.TrimEnd('/')}/v1/external/auth/login";
            using var response = await _httpClient.PostAsJsonAsync(
                url,
                new LoginRequest(_options.Email, _options.Password),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Shiprocket authentication failed with status {StatusCode}; using fallback shipping rates.",
                    response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct);
            if (string.IsNullOrWhiteSpace(result?.Token))
            {
                _logger.LogWarning("Shiprocket authentication response did not contain a token; using fallback shipping rates.");
                return null;
            }

            _token = result.Token;
            return _token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private IReadOnlyList<CourierQuote>? ParseQuotes(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data)
     || !data.TryGetProperty("available_courier_companies", out var companies)
     || companies.ValueKind != JsonValueKind.Array)
        {
            // Shiprocket sends an error message (HTTP 200) for pincodes it doesn't know.
            if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                _logger.LogWarning("Shiprocket says this pincode is not serviceable: {Message}", message.GetString());
                return Array.Empty<CourierQuote>();   // answered, but nothing delivers there
            }

            _logger.LogWarning("Shiprocket serviceability response had an unexpected shape; using fallback shipping rates.");
            return null;                               // unknown reply: keep checkout working
        }

        var quotes = new List<CourierQuote>();
        foreach (var company in companies.EnumerateArray())
        {
            if (!TryGetDecimal(company, "rate", out var rate)
    || !TryGetDecimal(company, "cod_charges", out var codCharges)
    || !TryGetEstimatedDays(company, out var days))
            {
                _logger.LogWarning("Shiprocket returned a courier result with missing or invalid rate data; using fallback shipping rates.");
                return null;
            }

            quotes.Add(new CourierQuote(rate, codCharges, days, GetSupportsCod(company)));
        }

        return quotes;
    }

    private static bool TryGetDecimal(JsonElement element, string propertyName, out decimal value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var property)) return false;

        if (property.ValueKind == JsonValueKind.Number)
            return property.TryGetDecimal(out value);

        return property.ValueKind == JsonValueKind.String
            && decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetEstimatedDays(JsonElement element, out int days)
    {
        days = 0;
        if (!element.TryGetProperty("estimated_delivery_days", out var property)) return false;

        if (property.ValueKind == JsonValueKind.Number)
            return property.TryGetInt32(out days);

        if (property.ValueKind != JsonValueKind.String) return false;

        var leadingDigits = new string((property.GetString() ?? "").TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(leadingDigits, NumberStyles.None, CultureInfo.InvariantCulture, out days);
    }

    private static bool GetSupportsCod(JsonElement company)
    {
        if (!company.TryGetProperty("cod", out var property)) return false;
        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => property.TryGetInt32(out var value) && value == 1,
            JsonValueKind.String => string.Equals(property.GetString(), "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(property.GetString(), "true", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private sealed record LoginRequest(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("password")] string Password);

    private sealed record LoginResponse(
        [property: JsonPropertyName("token")] string? Token);
}
