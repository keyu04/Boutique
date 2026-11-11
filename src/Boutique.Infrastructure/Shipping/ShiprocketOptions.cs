namespace Boutique.Infrastructure.Shipping;

public class ShiprocketOptions
{
    public const string Section = "Shiprocket";

    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string PickupPincode { get; set; } = "";
    public string BaseUrl { get; set; } = "https://apiv2.shiprocket.in";
}
