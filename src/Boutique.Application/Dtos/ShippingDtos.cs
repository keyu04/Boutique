namespace Boutique.Application.Dtos;

public record ShippingEstimateRequest(
    string Pincode,
    string PaymentMethod, // "Prepaid" or "Cod"
    List<ShippingEstimateItem> Items);

public record ShippingEstimateItem(int VariantId, int Quantity);

public record ShippingEstimateResponse(decimal Charge, string Eta, decimal ChargeableWeightKg);