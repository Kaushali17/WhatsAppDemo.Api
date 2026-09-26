using System.ComponentModel.DataAnnotations;

namespace WhatsAppDemo.Api.Models
{
    /// <summary>
    /// Request to send an approved WhatsApp template message via the Meta Cloud API.
    /// The access token is never accepted from the request; it comes from server config only.
    /// </summary>
    public class SendTemplateMessageRequest
    {
        [Required]
        [Phone]
        public string RecipientPhoneNumber { get; set; } = string.Empty;

        [Required]
        public string TemplateName { get; set; } = string.Empty;

        [Required]
        public string LanguageCode { get; set; } = "en_US";
    }
}
