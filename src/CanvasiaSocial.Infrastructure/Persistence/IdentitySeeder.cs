using CanvasiaSocial.Application.Common.Security;
using CanvasiaSocial.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanvasiaSocial.Infrastructure.Persistence;

public static class IdentitySeeder
{
    public static async Task MigrateAndSeedIdentityAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in ApplicationRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                EnsureSucceeded(result, $"'{role}' rolü oluşturulamadı.");
            }
        }

        var email = Environment.GetEnvironmentVariable("INITIAL_ADMIN_EMAIL");
        var password = Environment.GetEnvironmentVariable("INITIAL_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            EnsureSucceeded(await userManager.CreateAsync(user, password), "İlk admin hesabı oluşturulamadı.");
        }

        if (!await userManager.IsInRoleAsync(user, ApplicationRoles.Admin))
        {
            EnsureSucceeded(
                await userManager.AddToRoleAsync(user, ApplicationRoles.Admin),
                "İlk admin hesabına Admin rolü atanamadı.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"{message} {string.Join(" ", result.Errors.Select(x => x.Description))}");
        }
    }
}
