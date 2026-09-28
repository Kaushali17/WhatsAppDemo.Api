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
            await SeedExistingTestNumberAsync(db, configuration);
        }

        private static async Task SeedSalesUsersAndCustomersAsync(AppDbContext db)
        {
            if (await db.SalesUsers.AnyAsync())
            {
                // Already seeded.
                return;
            }

            var rahul = new SalesUser { Id = Guid.NewGuid(), Name = "Rahul", Email = "rahul@crm.local" };
            var priya = new SalesUser { Id = Guid.NewGuid(), Name = "Priya", Email = "priya@crm.local" };
            var amit = new SalesUser { Id = Guid.NewGuid(), Name = "Amit", Email = "amit@crm.local" };
            var neha = new SalesUser { Id = Guid.NewGuid(), Name = "Neha", Email = "neha@crm.local" };
            var jay = new SalesUser { Id = Guid.NewGuid(), Name = "Jay", Email = "jay@crm.local" };

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

        /// <summary>
        /// Migrates the existing single-number POC configuration into the new
        /// WhatsAppBusinessNumber table and assigns it to a test sales person, so the
        /// current test number keeps working after the multi-number changes.
        /// </summary>
        private static async Task SeedExistingTestNumberAsync(AppDbContext db, IConfiguration? configuration)
        {
            if (configuration is null)
            {
                return;
            }

            var phoneNumberId = configuration["WhatsApp:PhoneNumberId"];
            if (string.IsNullOrWhiteSpace(phoneNumberId))
            {
                return;
            }

            if (await db.WhatsAppBusinessNumbers.AnyAsync(n => n.PhoneNumberId == phoneNumberId))
            {
                return;
            }

            var testUser = await db.SalesUsers.OrderBy(u => u.Name).FirstOrDefaultAsync();
            if (testUser is null)
            {
                return;
            }

            db.WhatsAppBusinessNumbers.Add(new WhatsAppBusinessNumber
            {
                Id = Guid.NewGuid(),
                SalesUserId = testUser.Id,
                PhoneNumberId = phoneNumberId,
                DisplayPhoneNumber = configuration["WhatsApp:DisplayPhoneNumber"] ?? "+91 97258 62006",
                WabaId = configuration["WhatsApp:WabaId"],
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }
    }
}
