namespace WhatsAppDemo.Api.Models
{
    public class ChatMessages
    {
        public Guid Id { get; set; }

        public Guid ConversationId { get; set; }

        public string Direction { get; set; } = string.Empty;
        // Incoming / Outgoing

        public string MessageType { get; set; } = "Text";
        // Text / Image / Document

        public string? MessageText { get; set; }

        public string? MediaUrl { get; set; }

        public string? WhatsAppMessageId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
