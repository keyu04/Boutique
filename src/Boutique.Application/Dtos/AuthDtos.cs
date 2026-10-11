namespace Boutique.Application.Dtos;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, string DisplayName, IList<string> Roles);