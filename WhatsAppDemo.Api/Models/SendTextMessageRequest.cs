using System.ComponentModel.DataAnnotations;

namespace WhatsAppDemo.Api.Models
{
    /// <summary>
    /// Request to send a free-form WhatsApp text message via the Meta Cloud API.
    /// Note: outside the 24-hour customer service window, Meta only allows template messages.
    /// The access token is never accepted from the request; it comes from server config only.
    /// </summary>
    public class SendTextMessageRequest
    {
        [Required]
        [Phone]
        public string RecipientPhoneNumber { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;
    }
}
