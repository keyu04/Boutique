using Boutique.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Boutique.Infrastructure;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Categories.AnyAsync()) return; // already seeded

        var kurtis = new Category { Name = "Kurtis", Slug = "kurtis" };
        var kurtaSets = new Category { Name = "Kurta Sets", Slug = "kurta-sets" };
        var coords = new Category { Name = "Co-ords", Slug = "co-ords" };
        var dresses = new Category { Name = "Dresses", Slug = "dresses" };
        db.Categories.AddRange(kurtis, kurtaSets, coords, dresses);

        var products = new[]
        {
            new Product {
                Name = "Maroon Straight Cotton Kurti", Slug = "maroon-straight-cotton-kurti",
                Category = kurtis, Color = "Maroon", Fabric = "Cotton",
                Price = 899, Mrp = 1299, CostPrice = 530, WeightKg = 0.35m, Tag = "Bestseller",
                Variants = new List<ProductVariant> {
                    new() { Size = "S", StockQuantity = 6 }, new() { Size = "M", StockQuantity = 12 },
                    new() { Size = "L", StockQuantity = 9 }, new() { Size = "XL", StockQuantity = 5 },
                    new() { Size = "XXL", StockQuantity = 3 }
                },
                Images = new List<ProductImage> {
                    new() { Url = "https://images.unsplash.com/photo-1610030469983-98e550d6193c?w=850&q=85", IsPrimary = true, SortOrder = 0 }
                }
            },
            new Product {
                Name = "Rose Printed Everyday Kurti", Slug = "rose-printed-everyday-kurti",
                Category = kurtis, Color = "Rose", Fabric = "Cotton blend",
                Price = 949, Mrp = 1399, CostPrice = 580, WeightKg = 0.38m, Tag = "New arrival",
                Variants = new List<ProductVariant> {
                    new() { Size = "S", StockQuantity = 3 }, new() { Size = "M", StockQuantity = 7 },
                    new() { Size = "L", StockQuantity = 10 }, new() { Size = "XL", StockQuantity = 4 },
                    new() { Size = "XXL", StockQuantity = 2 }
                },
                Images = new List<ProductImage> {
                    new() { Url = "https://images.unsplash.com/photo-1585487000160-6ebcfceb0d03?w=850&q=85", IsPrimary = true, SortOrder = 0 }
                }
            },
            new Product {
                Name = "Olive A-Line Kurti", Slug = "olive-a-line-kurti",
                Category = kurtis, Color = "Olive", Fabric = "Cotton",
                Price = 999, Mrp = 1499, CostPrice = 610, WeightKg = 0.4m, Tag = "Trending",
                Variants = new List<ProductVariant> {
                    new() { Size = "S", StockQuantity = 4 }, new() { Size = "M", StockQuantity = 6 },
                    new() { Size = "L", StockQuantity = 8 }, new() { Size = "XL", StockQuantity = 5 },
                    new() { Size = "XXL", StockQuantity = 2 }
                },
                Images = new List<ProductImage> {
                    new() { Url = "https://images.unsplash.com/photo-1539008835657-9e8e9680c956?w=850&q=85", IsPrimary = true, SortOrder = 0 }
                }
            },
            new Product {
                Name = "Mustard Festive Kurta Set", Slug = "mustard-festive-kurta-set",
                Category = kurtaSets, Color = "Mustard", Fabric = "Cotton blend",
                Price = 1499, Mrp = 1999, CostPrice = 890, WeightKg = 0.7m, Tag = "Festive",
                Variants = new List<ProductVariant> {
                    new() { Size = "S", StockQuantity = 4 }, new() { Size = "M", StockQuantity = 5 },
                    new() { Size = "L", StockQuantity = 6 }, new() { Size = "XL", StockQuantity = 3 },
                    new() { Size = "XXL", StockQuantity = 1 }
                },
                Images = new List<ProductImage> {
                    new() { Url = "https://images.unsplash.com/photo-1595777457583-95e059d581b8?w=850&q=85", IsPrimary = true, SortOrder = 0 }
                }
            },
            new Product {
                Name = "Blush Co-ord Set", Slug = "blush-co-ord-set",
                Category = coords, Color = "Blush", Fabric = "Cotton blend",
                Price = 1299, Mrp = 1799, CostPrice = 780, WeightKg = 0.65m, Tag = "Popular",
                Variants = new List<ProductVariant> {
                    new() { Size = "S", StockQuantity = 4 }, new() { Size = "M", StockQuantity = 5 },
                    new() { Size = "L", StockQuantity = 5 }, new() { Size = "XL", StockQuantity = 3 },
                    new() { Size = "XXL", StockQuantity = 2 }
                },
                Images = new List<ProductImage> {
                    new() { Url = "https://images.unsplash.com/photo-1483985988355-763728e1935b?w=850&q=85", IsPrimary = true, SortOrder = 0 }
                }
            },
            new Product {
                Name = "Evening Floral Dress", Slug = "evening-floral-dress",
                Category = dresses, Color = "Floral", Fabric = "Viscose",
                Price = 1199, Mrp = 1699, CostPrice = 690, WeightKg = 0.5m, Tag = "New arrival",
                Variants = new List<ProductVariant> {
                    new() { Size = "S", StockQuantity = 3 }, new() { Size = "M", StockQuantity = 7 },
                    new() { Size = "L", StockQuantity = 6 }, new() { Size = "XL", StockQuantity = 4 },
                    new() { Size = "XXL", StockQuantity = 2 }
                },
                Images = new List<ProductImage> {
                    new() { Url = "https://images.unsplash.com/photo-1515372039744-b8f02a3ae446?w=850&q=85", IsPrimary = true, SortOrder = 0 }
                }
            },
        };

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }
    public static async Task SeedIdentityAsync(
    RoleManager<IdentityRole<int>> roleManager,
    UserManager<AdminUser> userManager)
    {
        foreach (var role in new[] { "Admin", "Staff" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<int>(role));

        if (await userManager.FindByEmailAsync("admin@velora.test") is null)
        {
            var admin = new AdminUser { UserName = "admin@velora.test", Email = "admin@velora.test", DisplayName = "Keyur" };
            var result = await userManager.CreateAsync(admin, "Admin@12345"); // dev password only
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}