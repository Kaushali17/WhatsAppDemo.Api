namespace WhatsAppDemo.Api.Models
{
    public class Customer
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string WhatsAppNumber { get; set; } = string.Empty;

        public Guid? AssignedUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public SalesUser? AssignedUser { get; set; }

        public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    }
}
