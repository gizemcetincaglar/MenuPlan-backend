using Menulux.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Menulux.Api.Data;

public static class SeedData
{
    public static async Task RunAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { "SuperAdmin", "RestaurantAdmin", "Kitchen", "Waiter", "Cashier" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var isDevelopment = services.GetRequiredService<IHostEnvironment>().IsDevelopment();
        var config = services.GetRequiredService<IConfiguration>();

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var superAdminEmail = config["Seed:SuperAdminEmail"] ?? "admin@menulux.local";
        var superAdminPassword = config["Seed:SuperAdminPassword"] ?? (isDevelopment ? "ChangeMe123!" : null);
        if (superAdminPassword is not null && await userManager.FindByEmailAsync(superAdminEmail) is null)
        {
            var superAdmin = new ApplicationUser
            {
                UserName = superAdminEmail,
                Email = superAdminEmail,
                FullName = "Super Admin",
            };
            await userManager.CreateAsync(superAdmin, superAdminPassword);
            await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
        }

        // Demo restoran ve kullanıcılar sadece geliştirme ortamında oluşturulur.
        if (!isDevelopment)
        {
            return;
        }

        var restaurant = await db.Restaurants.FirstOrDefaultAsync();
        if (restaurant is null)
        {
            restaurant = new Restaurant { Name = "Örnek Restoran", Slug = "ornek-restoran" };
            db.Restaurants.Add(restaurant);
            await db.SaveChangesAsync();
        }

        const string demoAdminEmail = "restoran@menulux.local";
        if (await userManager.FindByEmailAsync(demoAdminEmail) is null)
        {
            var demoAdmin = new ApplicationUser
            {
                UserName = demoAdminEmail,
                Email = demoAdminEmail,
                FullName = "Restoran Yöneticisi",
                RestaurantId = restaurant.Id,
            };
            await userManager.CreateAsync(demoAdmin, "ChangeMe123!");
            await userManager.AddToRoleAsync(demoAdmin, "RestaurantAdmin");
        }

        const string demoKitchenEmail = "mutfak@menulux.local";
        if (await userManager.FindByEmailAsync(demoKitchenEmail) is null)
        {
            var demoKitchen = new ApplicationUser
            {
                UserName = demoKitchenEmail,
                Email = demoKitchenEmail,
                FullName = "Mutfak",
                RestaurantId = restaurant.Id,
            };
            await userManager.CreateAsync(demoKitchen, "ChangeMe123!");
            await userManager.AddToRoleAsync(demoKitchen, "Kitchen");
        }

        const string demoWaiterEmail = "garson@menulux.local";
        if (await userManager.FindByEmailAsync(demoWaiterEmail) is null)
        {
            var demoWaiter = new ApplicationUser
            {
                UserName = demoWaiterEmail,
                Email = demoWaiterEmail,
                FullName = "Garson",
                RestaurantId = restaurant.Id,
            };
            await userManager.CreateAsync(demoWaiter, "ChangeMe123!");
            await userManager.AddToRoleAsync(demoWaiter, "Waiter");
        }

        const string demoCashierEmail = "kasiyer@menulux.local";
        if (await userManager.FindByEmailAsync(demoCashierEmail) is null)
        {
            var demoCashier = new ApplicationUser
            {
                UserName = demoCashierEmail,
                Email = demoCashierEmail,
                FullName = "Kasiyer",
                RestaurantId = restaurant.Id,
            };
            await userManager.CreateAsync(demoCashier, "ChangeMe123!");
            await userManager.AddToRoleAsync(demoCashier, "Cashier");
        }
    }
}
