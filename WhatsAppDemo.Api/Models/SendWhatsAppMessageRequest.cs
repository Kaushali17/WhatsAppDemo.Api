namespace WhatsAppDemo.Api.Models
{
    public class SendWhatsAppMessageRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Message { get; set; }

        // Text / Image / Document
        public string MessageType { get; set; } = "Text";

        public string? MediaUrl { get; set; }
    }
}
