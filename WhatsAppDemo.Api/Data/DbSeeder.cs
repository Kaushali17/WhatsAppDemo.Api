using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WhatsAppDemo.Api.Models;

namespace WhatsAppDemo.Api.Data
{
    /// <summary>
    /// Seeds initial POC data at runtime. This intentionally avoids EF Core's
    /// HasData/model seeding so that no new migration is required.
    /// </summary>
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext db, IConfiguration? configuration = null)
        {
            await SeedSalesUsersAndCustomersAsync(db);
            await EnsureAdminRoleAsync(db);
            // NOTE: Automatic seeding of the Meta test WhatsApp number has been
            // intentionally disabled. Outbound messaging now requires a real
            // WhatsApp Business number selected via WhatsAppBusinessNumberId.
            // Existing real business-number records are preserved.
        }

        /// <summary>
        /// Ensures the CRM Admin user (Amit) has the "Admin" role without creating a
        /// duplicate user or requiring a data-only migration. Runs on every startup so
        /// existing databases (seeded before the Role column existed) are corrected.
        /// </summary>
        private static async Task EnsureAdminRoleAsync(AppDbContext db)
        {
            var adminId = Guid.Parse("C5DC5BCC-524C-4391-BC0E-513A4A24B700");

            var admin = await db.SalesUsers.FirstOrDefaultAsync(u => u.Id == adminId)
                ?? await db.SalesUsers.FirstOrDefaultAsync(u => u.Name == "Amit");

            if (admin is not null && admin.Role != "Admin")
            {
                admin.Role = "Admin";
                await db.SaveChangesAsync();
            }
        }

        private static async Task SeedSalesUsersAndCustomersAsync(AppDbContext db)
        {
            if (await db.SalesUsers.AnyAsync())
            {
                // Already seeded.
                return;
            }

            var rahul = new SalesUser { Id = Guid.NewGuid(), Name = "Rahul", Email = "rahul@crm.local", Role = "Salesperson" };
            var priya = new SalesUser { Id = Guid.NewGuid(), Name = "Priya", Email = "priya@crm.local", Role = "Salesperson" };
            var amit = new SalesUser { Id = Guid.Parse("C5DC5BCC-524C-4391-BC0E-513A4A24B700"), Name = "Amit", Email = "amit@crm.local", Role = "Admin" };
            var neha = new SalesUser { Id = Guid.NewGuid(), Name = "Neha", Email = "neha@crm.local", Role = "Salesperson" };
            var jay = new SalesUser { Id = Guid.NewGuid(), Name = "Jay", Email = "jay@crm.local", Role = "Salesperson" };

            var salesUsers = new[] { rahul, priya, amit, neha, jay };
            await db.SalesUsers.AddRangeAsync(salesUsers);

            var customers = new[]
            {
                new Customer { Id = Guid.NewGuid(), Name = "Acme Corp", WhatsAppNumber = "919000000001", AssignedUserId = rahul.Id, CreatedAt = DateTime.UtcNow },
                new Customer { Id = Guid.NewGuid(), Name = "Globex",    WhatsAppNumber = "919000000002", AssignedUserId = priya.Id, CreatedAt = DateTime.UtcNow },
                new Customer { Id = Guid.NewGuid(), Name = "Initech",   WhatsAppNumber = "919000000003", AssignedUserId = amit.Id,  CreatedAt = DateTime.UtcNow },
                new Customer { Id = Guid.NewGuid(), Name = "Umbrella",  WhatsAppNumber = "919000000004", AssignedUserId = neha.Id,  CreatedAt = DateTime.UtcNow },
                new Customer { Id = Guid.NewGuid(), Name = "Soylent",   WhatsAppNumber = "919000000005", AssignedUserId = jay.Id,   CreatedAt = DateTime.UtcNow },
                new Customer { Id = Guid.NewGuid(), Name = "Hooli",     WhatsAppNumber = "919000000006", AssignedUserId = rahul.Id, CreatedAt = DateTime.UtcNow }
            };

            await db.Customers.AddRangeAsync(customers);
            await db.SaveChangesAsync();
        }
    }
}
