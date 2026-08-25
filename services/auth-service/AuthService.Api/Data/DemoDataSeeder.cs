using AuthService.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;
using Shared.Infrastructure.Auth;

namespace AuthService.Api.Data;

/// <summary>
/// Seeds one demo account per role for local/dev use. Guarded by config flag "Seed:Enabled"
/// (true only in appsettings.Development.json) — never runs in production. Current user base is
/// test/demo data only, so there is no migration path from the old Supabase Auth users to
/// preserve; this seeder is the replacement starting point.
/// </summary>
public static class DemoDataSeeder
{
    private const string DemoPassword = "Demo123!";

    public static async Task SeedAsync(AuthDbContext db)
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }

        db.Users.AddRange(
            new User { Email = "student@demo.nnpl", Name = "Học viên Demo", Role = Roles.Student, PasswordHash = PasswordHasher.Hash(DemoPassword) },
            new User { Email = "teacher@demo.nnpl", Name = "Giáo viên Demo", Role = Roles.Teacher, PasswordHash = PasswordHasher.Hash(DemoPassword) },
            new User { Email = "admin@demo.nnpl", Name = "Quản trị viên Demo", Role = Roles.Admin, PasswordHash = PasswordHasher.Hash(DemoPassword) });

        await db.SaveChangesAsync();
    }
}
