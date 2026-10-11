using Boutique.Application.Dtos;

namespace Boutique.Application.Interfaces;

public interface IShippingService
{
    (decimal charge, string eta, decimal chargeableWeightKg) Estimate(decimal totalWeightKg, string pincode, string paymentMethod);
}