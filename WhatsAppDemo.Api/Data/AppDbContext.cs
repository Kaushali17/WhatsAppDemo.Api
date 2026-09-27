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

        public DbSet<WhatsAppBusinessNumber> WhatsAppBusinessNumbers => Set<WhatsAppBusinessNumber>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // SalesUser -> Customers
            modelBuilder.Entity<Customer>()
                .HasOne(c => c.AssignedUser)
                .WithMany(u => u.Customers)
                .HasForeignKey(c => c.AssignedUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Customer -> Conversations
            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.Customer)
                .WithMany(cust => cust.Conversations)
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            // SalesUser -> Conversations
            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.AssignedUser)
                .WithMany(u => u.Conversations)
                .HasForeignKey(c => c.AssignedUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Conversation -> ChatMessages
            modelBuilder.Entity<ChatMessages>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.NoAction);

            // SalesUser -> WhatsAppBusinessNumbers
            modelBuilder.Entity<WhatsAppBusinessNumber>()
                .HasOne(n => n.SalesUser)
                .WithMany(u => u.WhatsAppBusinessNumbers)
                .HasForeignKey(n => n.SalesUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // A registered PhoneNumberId must be unique so incoming webhooks map to
            // exactly one business number/sales person.
            modelBuilder.Entity<WhatsAppBusinessNumber>()
                .HasIndex(n => n.PhoneNumberId)
                .IsUnique();

            // WhatsAppBusinessNumber -> Conversations
            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.WhatsAppBusinessNumber)
                .WithMany(n => n.Conversations)
                .HasForeignKey(c => c.WhatsAppBusinessNumberId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
