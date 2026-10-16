using CareTogether.Domain;
using Microsoft.EntityFrameworkCore;

namespace CareTogether.Infrastructure;

public static class SeedData
{
    public static async Task InitializeAsync(CareDbContext db, PasswordService passwords)
    {
        await db.Database.MigrateAsync();
    }
}
