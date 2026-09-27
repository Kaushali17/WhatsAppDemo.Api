namespace WhatsAppDemo.Api.Models
{
    public class SendWhatsAppMessageRequest
    {
        /// <summary>
        /// Preferred. When supplied, the message is sent through the business number
        /// associated with this conversation, giving unambiguous multi-number routing.
        /// Required only when <see cref="PhoneNumber"/> is not provided.
        /// </summary>
        public Guid? ConversationId { get; set; }

        /// <summary>
        /// Legacy. The customer's WhatsApp number. Still supported for backward
        /// compatibility and for starting a message without a known conversation.
        /// Required only when <see cref="ConversationId"/> is not provided.
        /// </summary>
        public string? PhoneNumber { get; set; }

        public string? Message { get; set; }

        // Text / Image / Document
        public string MessageType { get; set; } = "Text";

        public string? MediaUrl { get; set; }
    }
}
