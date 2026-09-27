namespace WhatsAppDemo.Api.Models
{
    public class Conversation
    {
        public Guid Id { get; set; }

        public Guid CustomerId { get; set; }

        public Guid? AssignedUserId { get; set; }

        /// <summary>
        /// The WhatsApp Business number (Meta PhoneNumberId) this conversation is
        /// tied to. Outgoing messages must be sent through this number.
        /// </summary>
        public Guid? WhatsAppBusinessNumberId { get; set; }

        public string Status { get; set; } = "Open";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Customer? Customer { get; set; }

        public SalesUser? AssignedUser { get; set; }

        public WhatsAppBusinessNumber? WhatsAppBusinessNumber { get; set; }

        public ICollection<ChatMessages> Messages { get; set; } = new List<ChatMessages>();
    }
}
