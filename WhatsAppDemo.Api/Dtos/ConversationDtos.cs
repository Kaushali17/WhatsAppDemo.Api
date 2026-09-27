namespace WhatsAppDemo.Api.Dtos
{
    public class ConversationDto
    {
        public Guid Id { get; set; }

        public Guid CustomerId { get; set; }

        public string? CustomerName { get; set; }

        public string? CustomerWhatsAppNumber { get; set; }

        public Guid? AssignedUserId { get; set; }

        public string? AssignedUserName { get; set; }

        public Guid? WhatsAppBusinessNumberId { get; set; }

        public string? WhatsAppDisplayPhoneNumber { get; set; }

        public string Status { get; set; } = "Open";

        public DateTime CreatedAt { get; set; }

        public DateTime LastMessageAt { get; set; }

        public string? LastMessagePreview { get; set; }
    }

    public class ChatMessageDto
    {
        public Guid Id { get; set; }

        public Guid ConversationId { get; set; }

        public string Direction { get; set; } = string.Empty;

        public string MessageType { get; set; } = "Text";

        public string? MessageText { get; set; }

        public string? MediaUrl { get; set; }

        public string? WhatsAppMessageId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
