using Boutique.Domain;

namespace Boutique.Application.Interfaces;

public interface ITokenService
{
    (string token, DateTime expiresAtUtc) CreateAccessToken(AdminUser user, IList<string> roles);
    string GenerateRefreshToken();
    string Hash(string raw);
}