using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StajEvrak.Application.Abstractions;
using StajEvrak.Application.Auth;
using StajEvrak.Domain.Users;

namespace StajEvrak.Infrastructure.Data;

/// <summary>Veritabanını günceller (migration) ve ilk admin hesabını oluşturur.</summary>
public sealed class DbSeeder(AppDbContext db, IPasswordService passwords, IClock clock, IConfiguration configuration, ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        if (await db.Admins.AnyAsync(ct)) return;

        var userName = configuration["Seed:AdminUserName"] ?? "admin";
        var email = configuration["Seed:AdminEmail"] ?? "admin@stajevrak.local";
        var password = configuration["Seed:AdminPassword"] ?? "Admin12345";

        var admin = new Admin(userName, email, clock.UtcNow);
        admin.SetPasswordHash(passwords.Hash(admin, password));
        db.Admins.Add(admin);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Varsayılan admin hesabı oluşturuldu: {UserName}", userName);
    }
}
