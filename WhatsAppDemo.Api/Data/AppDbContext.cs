using Microsoft.EntityFrameworkCore;
using WhatsAppDemo.Api.Models;

namespace WhatsAppDemo.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
        {
        }

        public DbSet<SalesUser> SalesUsers => Set<SalesUser>();

        public DbSet<Customer> Customers => Set<Customer>();

        public DbSet<Conversation> Conversations => Set<Conversation>();

        public DbSet<ChatMessages> ChatMessages => Set<ChatMessages>();
    }
}
