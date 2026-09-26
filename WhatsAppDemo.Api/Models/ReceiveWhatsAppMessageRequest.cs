using System.ComponentModel.DataAnnotations;

namespace WhatsAppDemo.Api.Models
{
    /// <summary>
    /// Simulates an incoming WhatsApp message payload delivered by Meta's webhook.
    /// </summary>
    public class ReceiveWhatsAppMessageRequest
    {
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Message { get; set; }

        // Text / Image / Document
        public string MessageType { get; set; } = "Text";

        public string? MediaUrl { get; set; }

        /// <summary>
        /// Meta media id for incoming media messages. When set, the file must be
        /// downloaded from Meta's Media API and stored locally before it can be
        /// opened/downloaded from the CRM.
        /// </summary>
        public string? MetaMediaId { get; set; }

        /// <summary>
        /// Original file name supplied by WhatsApp for incoming documents.
        /// </summary>
        public string? FileName { get; set; }

        public string? WhatsAppMessageId { get; set; }

        /// <summary>
        /// Optional customer name used when a new customer must be created.
        /// </summary>
        public string? CustomerName { get; set; }
    }
}
