namespace WhatsAppDemo.Api.Models
{
    public class WhatsAppWebhookRequest
    {
        public string? MessageId { get; set; }

        public string? From { get; set; }

        public string? Message { get; set; }

        public string? Type { get; set; }

        public DateTime? Timestamp { get; set; }

        public string? MediaUrl { get; set; }

        public string? FileName { get; set; }
    }
}
