using Boutique.Application.Interfaces;
using Boutique.Domain;
using Boutique.Infrastructure;
using Boutique.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Boutique.Application.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.FileProviders;
using System.Threading.RateLimiting;
using Boutique.Infrastructure.Shipping;
using Boutique.Application.Shipping;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// cors
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddPolicy("web", policy => policy
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));   // needed so the browser sends the refresh-token cookie

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<IShippingService, RuleBasedShippingService>();
builder.Services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<ITokenService, TokenService>();

if (builder.Configuration["ImageStorage:Provider"] == "S3")
    builder.Services.AddSingleton<IImageStorage, S3ImageStorage>();
else
    builder.Services.AddSingleton<IImageStorage, LocalDiskImageStorage>();

builder.Services.Configure<ShippingPolicyOptions>(builder.Configuration.GetSection(ShippingPolicyOptions.Section));
builder.Services.Configure<ShiprocketOptions>(builder.Configuration.GetSection(ShiprocketOptions.Section));
builder.Services.AddHttpClient<ICourierRateProvider, ShiprocketClient>();
builder.Services.AddScoped<IShippingQuoteService, ShippingQuoteService>();
builder.Services.AddMemoryCache();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();   // KnownNetworks on older SDKs
    o.KnownProxies.Clear();
    o.ForwardLimit = 2;          // Cloudflare -> Render: skip our own proxy hop, take the real client
});

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

builder.Services.AddIdentityCore<AdminUser>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
}).AddRoles<IdentityRole<int>>()
  .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("track", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    options.AddPolicy("estimate", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();

    // demo catalogue: always in Development, in Production only if Seed__Demo=true
    if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Seed:Demo"))
        await DbSeeder.SeedAsync(db);

    await DbSeeder.SeedIdentityAsync(roleManager, userManager, builder.Configuration["Seed:AdminPassword"] ?? "");
    await DbSeeder.SeedStaffAsync(userManager, builder.Configuration["Seed:StaffPassword"] ?? "");
}
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));


app.UseHttpsRedirection();

app.UseCors("web");
app.UseRateLimiter();
var uploadsPath = Path.GetFullPath(builder.Configuration["ImageStorage:LocalFolder"] ?? "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
