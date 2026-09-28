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

        /// <summary>
        /// Delivery status reported by Meta's status webhooks:
        /// "sent", "delivered", "read", "failed". Defaults to "sent" for outgoing
        /// messages accepted by Meta; null for incoming messages.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// The approved WhatsApp template name used for business-initiated messages.
        /// Null for free-form text/media messages.
        /// </summary>
        public string? TemplateName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public Conversation? Conversation { get; set; }
    }
}
