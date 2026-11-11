namespace Boutique.Application.Dtos;

public record ShippingEstimateRequest(
    string Pincode,
    string PaymentMethod, // "Prepaid" or "Cod"
    List<ShippingEstimateItem> Items);

public record ShippingEstimateItem(int VariantId, int Quantity);

public record ShippingEstimateResponse(
    decimal Charge, string Eta, decimal ChargeableWeightKg,
    decimal StandardCharge = 0, decimal Saving = 0,
    decimal AmountToFreeDelivery = 0, decimal? FreeDeliveryAtSubtotal = null,
    decimal? PrepaidSaving = null, string? DeliveryBy = null);