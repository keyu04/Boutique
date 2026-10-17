namespace Boutique.Application.Dtos;

public record ReportSummaryDto(
    DateOnly From,
    DateOnly To,
    int OrderCount,
    decimal ItemRevenue,
    decimal ShippingCollected,
    decimal Cost,
    decimal Margin,
    decimal MarginPercent,
    Dictionary<string, int> OrdersByStatus);