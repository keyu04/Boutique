using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boutique.Tests;

// Verifies refresh-token rotation, reuse detection and logout against the real API pipeline.
// Cookies are handled manually (HandleCookies = false) so each request sends exactly the
// token we choose, the same way the curl test did.
public class RefreshTokenTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _adminPassword;

    public RefreshTokenTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        _adminPassword = factory.Services.GetRequiredService<IConfiguration>()["Seed:AdminPassword"]
            ?? throw new InvalidOperationException("Set Seed:AdminPassword in user-secrets for Boutique.Api.");
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_issues_a_new_access_token()
    {
        var cookieA = await LoginAndGetCookieAsync();

        var refresh = await RefreshAsync(cookieA);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        var cookieB = ExtractRefreshCookie(refresh);
        Assert.NotEqual(cookieA, cookieB); // rotation: a new token every time

        var body = await refresh.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.False(string.IsNullOrWhiteSpace(body!["accessToken"]?.ToString()));
    }

    [Fact]
    public async Task Reusing_a_rotated_token_is_rejected_and_revokes_every_session()
    {
        var cookieA = await LoginAndGetCookieAsync();

        var first = await RefreshAsync(cookieA);            // A -> B (A is now revoked)
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var cookieB = ExtractRefreshCookie(first);

        var replay = await RefreshAsync(cookieA);           // attacker replays A
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        var legitimate = await RefreshAsync(cookieB);       // B was valid, but the whole family is revoked
        Assert.Equal(HttpStatusCode.Unauthorized, legitimate.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var cookie = await LoginAndGetCookieAsync();

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("Cookie", cookie);
        var logoutResponse = await _client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refresh = await RefreshAsync(cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    private async Task<string> LoginAndGetCookieAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@velora.test", password = _adminPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ExtractRefreshCookie(response);
    }

    private Task<HttpResponseMessage> RefreshAsync(string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", cookie);
        return _client.SendAsync(request);
    }

    // "refreshToken=ABC123; expires=...; path=/api/auth; ..." -> "refreshToken=ABC123"
    private static string ExtractRefreshCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("refreshToken=") && !c.StartsWith("refreshToken=;"))
            .Split(';')[0];
}