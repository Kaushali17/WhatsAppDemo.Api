using System.ComponentModel.DataAnnotations;

namespace WhatsAppDemo.Api.Dtos
{
    /// <summary>
    /// Request to send an approved WhatsApp template to an existing CRM customer.
    /// The customer's WhatsApp number, the sending business number and the access
    /// token are all resolved server-side; none are accepted from the caller.
    /// </summary>
    public class SendTemplateToCustomerRequest
    {
        [Required]
        public Guid CustomerId { get; set; }

        /// <summary>
        /// Optional. When supplied, the template is sent through this existing
        /// conversation's WhatsApp Business number (multi-number routing). When null,
        /// the customer's open conversation is reused or created.
        /// </summary>
        public Guid? ConversationId { get; set; }

        /// <summary>
        /// Optional. Explicitly selects the WhatsApp Business number (sender) to send
        /// from. Takes precedence over <see cref="ConversationId"/> for routing. When
        /// null, the sender is derived from the conversation, falling back to the
        /// globally configured number.
        /// </summary>
        public Guid? WhatsAppBusinessNumberId { get; set; }

        [Required]
        public string TemplateName { get; set; } = string.Empty;

        [Required]
        public string LanguageCode { get; set; } = "en_US";

        /// <summary>
        /// Positional body parameters for templates that contain placeholders such as
        /// {{1}}, {{2}}. Order matters. Leave empty for templates without variables.
        /// </summary>
        public List<TemplateParameterRequest> Parameters { get; set; } = new();
    }

    /// <summary>
    /// A single template parameter. Currently supports text body parameters, which
    /// covers the common {{n}} placeholder case.
    /// </summary>
    public class TemplateParameterRequest
    {
        /// <summary>
        /// Meta parameter type. Defaults to "text".
        /// </summary>
        public string Type { get; set; } = "text";

        [Required]
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response returned to the frontend after a template send attempt. Never
    /// contains the Meta access token or other credentials.
    /// </summary>
    public class SendTemplateMessageResponse
    {
        public bool Success { get; set; }

        /// <summary>Meta message id (e.g. "wamid....") when the send succeeds.</summary>
        public string? MessageId { get; set; }

        /// <summary>The conversation the outgoing message was stored against.</summary>
        public Guid? ConversationId { get; set; }

        /// <summary>Short, safe error summary when the send fails.</summary>
        public string? Error { get; set; }

        /// <summary>Additional safe detail (e.g. Meta's error message). No secrets.</summary>
        public string? Details { get; set; }
    }
}
