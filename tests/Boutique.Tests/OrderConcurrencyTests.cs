using System.Net;
using System.Net.Http.Json;
using Boutique.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boutique.Tests;

// Proves that two near-simultaneous orders for the last unit of stock
// cannot both succeed: optimistic concurrency (xmin) + a one-time retry
// must let exactly one order win and the other fail cleanly.
public class OrderConcurrencyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OrderConcurrencyTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }
    
    [Fact]
    public async Task Two_simultaneous_orders_for_the_last_item_only_one_succeeds()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Arrange: force a known variant down to exactly 1 unit of stock
        var variant = await db.ProductVariants.FirstAsync(v => v.Size == "XXL");
        variant.StockQuantity = 1;
        await db.SaveChangesAsync();

        // NEW: how many order lines exist for this variant BEFORE the race
        var itemsBefore = await db.OrderItems.AsNoTracking()
            .CountAsync(i => i.VariantId == variant.Id);

        var orderBody = new
        {
            items = new[] { new { variantId = variant.Id, quantity = 1 } },
            customerName = "Concurrency Test",
            phone = "9876543210",
            addressLine = "Test Address",
            city = "Surat",
            state = "Gujarat",
            pincode = "395006",
            paymentMethod = "Prepaid"
        };

        var client1 = _factory.CreateClient();
        var client2 = _factory.CreateClient();

        // Act: fire both requests at the same time
        var task1 = client1.PostAsJsonAsync("/api/orders", orderBody);
        var task2 = client2.PostAsJsonAsync("/api/orders", orderBody);
        var responses = await Task.WhenAll(task1, task2);

        // Assert: exactly one succeeded, the other was rejected
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var failureCount = responses.Count(r =>
            r.StatusCode == HttpStatusCode.Conflict || r.StatusCode == HttpStatusCode.BadRequest);
        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        // Assert: stock is 0, never negative
        var finalStock = await db.ProductVariants.AsNoTracking()
            .Where(v => v.Id == variant.Id)
            .Select(v => v.StockQuantity)
            .FirstAsync();
        Assert.Equal(0, finalStock);

        // NEW: exactly ONE new order line was created by the race
        var itemsAfter = await db.OrderItems.AsNoTracking()
            .CountAsync(i => i.VariantId == variant.Id);
        Assert.Equal(1, itemsAfter - itemsBefore);
    }
}