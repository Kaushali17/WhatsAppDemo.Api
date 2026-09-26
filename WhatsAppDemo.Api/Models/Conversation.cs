namespace WhatsAppDemo.Api.Models
{
    public class Conversation
    {
        public Guid Id { get; set; }

        public Guid CustomerId { get; set; }

        public Guid? AssignedUserId { get; set; }

        public string Status { get; set; } = "Open";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Customer? Customer { get; set; }

        public SalesUser? AssignedUser { get; set; }

        public ICollection<ChatMessages> Messages { get; set; } = new List<ChatMessages>();
    }
}
