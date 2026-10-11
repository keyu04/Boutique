namespace Boutique.Domain;

public enum OrderStatus { Pending, Confirmed, Packed, Shipped, Delivered, Cancelled }
public enum PaymentMethod { Prepaid, Cod }
public enum PaymentStatus { Pending, Paid, Failed, Refunded, CodDue }