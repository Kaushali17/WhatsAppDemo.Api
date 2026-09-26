namespace WhatsAppDemo.Api.Dtos
{
    /// <summary>
    /// Result returned by the WhatsApp service for send/receive operations.
    /// </summary>
    public class WhatsAppMessageResult
    {
        public bool Success { get; set; }

        /// <summary>
        /// True when the operation failed because the customer could not be found.
        /// Allows the controller to translate this into a 404 response.
        /// </summary>
        public bool CustomerNotFound { get; set; }

        public string Message { get; set; } = string.Empty;

        public ConversationDto? Conversation { get; set; }

        public ChatMessageDto? ChatMessage { get; set; }
    }
}
