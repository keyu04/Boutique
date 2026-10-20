public record AdminOrderListItemDto(
    int Id, string OrderNumber, DateTime CreatedAt, string Status, string PaymentMethod,
    string PaymentStatus, string CustomerName, string Phone, string City, decimal Total, int ItemCount);

public record AdminOrderListResponse(List<AdminOrderListItemDto> Items, int Page, int PageSize, int TotalCount);

public record AdminOrderItemDto(
    int ProductId, int VariantId, string ProductName, string Size,
    decimal UnitPrice, decimal? UnitCost, int Quantity, decimal LineTotal, string? ImageUrl);   // UnitCost: Admin only

public record AdminOrderHistoryDto(string From, string To, string ChangedBy, DateTime ChangedAt, string? Note);

public record AdminOrderDetailDto(
    int Id, string OrderNumber, DateTime CreatedAt, string Status, string PaymentMethod,
    string PaymentStatus, string? PaymentReference,
    string CustomerName, string Phone, string AddressLine, string City, string State, string Pincode,
    decimal Subtotal, decimal ShippingCharge, decimal Total,
    string? CourierName, string? TrackingNumber,
    List<string> AllowedNextStatuses,
    List<AdminOrderItemDto> Items, List<AdminOrderHistoryDto> History);

public record ChangeStatusRequest(string Status, string? Note);
public record SetTrackingRequest(string CourierName, string TrackingNumber);