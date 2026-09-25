using AlphaZero.Modules.Tenants.Domain;
using AlphaZero.Modules.Tenants.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Tenants.Infrastructure.Persistance.Seeding;

public static class TenantSeeder
{
    public static readonly Guid DemoTenantId = Guid.Parse("9ac6bf72-f911-452e-a43a-ae9b3e26238c");

    public static async Task SeedAsync(AppDbContext context)
    {
        var existingTenant = await context.Tenants.FirstOrDefaultAsync(t => t.Id == DemoTenantId);

        if (existingTenant is null)
        {
            var branding = new TenantBranding(
                primaryColor: "#0F172A",
                secondaryColor: "#10B981",
                logoUrl: "https://alphazero.academy/assets/logo.svg",
                darkModeLogoUrl: "https://alphazero.academy/assets/logo-dark.svg",
                faviconUrl: "https://alphazero.academy/favicon.ico");

            var tenant = Tenant.CreateWithId(
                DemoTenantId,
                name: "AlphaZero Learning Academy",
                subdomain: "alphazero",
                branding: branding);

            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();
        }
    }
}
