using Microsoft.EntityFrameworkCore;
using UniLMS.API.Models.Entities;

namespace UniLMS.API.Data;

public static class DbSeeder
{
    /// <summary>
    /// Seeds default admin user and the 4 academic levels if they don't exist.
    /// Call this after database migration.
    /// </summary>
    public static async Task SeedAsync(AppDbContext db, IConfiguration config)
    {
        await SeedAdminAsync(db, config);
        await SeedLevelsAsync(db);
    }

    // ── Admin ─────────────────────────────────────────────────────────────
    private static async Task SeedAdminAsync(AppDbContext db, IConfiguration config)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
            return;

        var adminEmail = config["Seed:AdminEmail"] ?? "admin@unilms.com";
        var adminPassword = config["Seed:AdminPassword"] ?? "Admin@123456";

        var admin = new User
        {
            Name = "System Admin",
            Email = adminEmail.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            Role = UserRole.Admin
        };

        db.Users.Add(admin);
        await db.SaveChangesAsync();

        Console.WriteLine($"[Seeder] Default admin created: {adminEmail}");
    }

    // ── Academic Levels ───────────────────────────────────────────────────
    private static async Task SeedLevelsAsync(AppDbContext db)
    {
        if (await db.Levels.AnyAsync())
            return;

        var levels = new[]
        {
            new LevelInfo
            {
                LevelNumber    = 1,
                Name           = "Level 1",
                Description    = "This level covers the basics of computer science and lays the foundation for all future studies.",
                HasDepartments = false
            },
            new LevelInfo
            {
                LevelNumber    = 2,
                Name           = "Level 2",
                Description    = "This level builds on the fundamentals, introducing core programming concepts and mathematics.",
                HasDepartments = false
            },
            new LevelInfo
            {
                LevelNumber    = 3,
                Name           = "Level 3",
                Description    = "This level focuses on software development and allows students to choose their specialization department.",
                HasDepartments = true
            },
            new LevelInfo
            {
                LevelNumber    = 4,
                Name           = "Level 4",
                Description    = "This level covers advanced topics and prepares students for graduation projects and the professional world.",
                HasDepartments = true
            }
        };

        db.Levels.AddRange(levels);
        await db.SaveChangesAsync();

        Console.WriteLine("[Seeder] 4 academic levels seeded.");
    }
}
