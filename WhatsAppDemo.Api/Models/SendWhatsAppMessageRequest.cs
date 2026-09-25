namespace WhatsAppDemo.Api.Models
{
    public class SendWhatsAppMessageRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
