using Boutique.Application.Interfaces;

namespace Boutique.Infrastructure.Services;

public class OrderNumberGenerator : IOrderNumberGenerator
{
    public string Generate() =>
        $"SJ-{DateTime.UtcNow:yyyy}-{Random.Shared.Next(100000, 999999)}";
}