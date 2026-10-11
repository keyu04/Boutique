using System.Security.Claims;
using Boutique.Application.Auth;
using Boutique.Application.Dtos;
using Boutique.Application.Interfaces;
using Boutique.Domain;
using Boutique.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Boutique.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string RefreshCookie = "refreshToken";

    private readonly UserManager<AdminUser> _users;
    private readonly ITokenService _tokens;
    private readonly AppDbContext _db;
    private readonly JwtSettings _jwt;
    private readonly IWebHostEnvironment _env;

    public AuthController(UserManager<AdminUser> users, ITokenService tokens,
        AppDbContext db, IOptions<JwtSettings> jwt, IWebHostEnvironment env)
    {
        _users = users;
        _tokens = tokens;
        _db = db;
        _jwt = jwt.Value;
        _env = env;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var invalid = Unauthorized(new { error = "Invalid email or password." });
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null) return invalid;

        if (await _users.IsLockedOutAsync(user))
            return StatusCode(429, new { error = "Too many failed attempts. Try again later." });

        if (!await _users.CheckPasswordAsync(user, req.Password))
        {
            await _users.AccessFailedAsync(user);
            return invalid;
        }

        await _users.ResetAccessFailedCountAsync(user);
        return Ok(await IssueSessionAsync(user));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var unauthorized = Unauthorized(new { error = "Session expired. Please log in again." });

        if (!Request.Cookies.TryGetValue(RefreshCookie, out var raw) || string.IsNullOrWhiteSpace(raw))
            return unauthorized;

        var hash = _tokens.Hash(raw);
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored is null) return unauthorized;

        if (stored.RevokedAt is not null)
        {
            // Only a token that was ROTATED AWAY and then replayed counts as theft
            if (stored.ReplacedByTokenHash is not null)
            {
                await _db.RefreshTokens
                    .Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));
            }
            Response.Cookies.Delete(RefreshCookie, CookieOptionsFor());
            return unauthorized;
        }

        if (stored.ExpiresAt <= DateTime.UtcNow || await _users.IsLockedOutAsync(stored.User))
            return unauthorized;

        return Ok(await IssueSessionAsync(stored.User, stored)); // rotate
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(RefreshCookie, out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            var hash = _tokens.Hash(raw);
            var stored = await _db.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null);
            if (stored is not null)
            {
                stored.RevokedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }
        Response.Cookies.Delete(RefreshCookie, CookieOptionsFor());
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        id = User.FindFirstValue("sub"),
        email = User.FindFirstValue("email"),
        roles = User.FindAll("role").Select(c => c.Value)
    });

    // Creates a new refresh token (revoking the previous one, if any) and a new access token.
    private async Task<LoginResponse> IssueSessionAsync(AdminUser user, RefreshToken? previous = null)
    {
        var raw = _tokens.GenerateRefreshToken();
        var hash = _tokens.Hash(raw);
        var expires = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays);

        if (previous is not null)
        {
            previous.RevokedAt = DateTime.UtcNow;
            previous.ReplacedByTokenHash = hash;
        }
        _db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = hash, ExpiresAt = expires });
        await _db.SaveChangesAsync(); // revoke-old + add-new in one save

        Response.Cookies.Append(RefreshCookie, raw, CookieOptionsFor(expires));

        var roles = await _users.GetRolesAsync(user);
        var (token, accessExpires) = _tokens.CreateAccessToken(user, roles);
        return new LoginResponse(token, accessExpires, user.DisplayName, roles);
    }

    private CookieOptions CookieOptionsFor(DateTime? expires = null) => new()
    {
        HttpOnly = true,
        Secure = !_env.IsDevelopment(), // plain http://localhost in dev; HTTPS-only everywhere else
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",             // browser sends it only to auth endpoints
        Expires = expires
    };
}