using System.Text.Json;

namespace WhatsAppDemo.Api.Dtos
{
    /// <summary>
    /// A real WhatsApp message template retrieved from Meta's WABA
    /// (/message_templates). Never contains the Meta access token or any credential.
    /// </summary>
    public class WhatsAppTemplateDto
    {
        /// <summary>Exact Meta template name. Sent verbatim to Meta when messaging.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Exact Meta language code (mapped from Meta's "language").</summary>
        public string LanguageCode { get; set; } = string.Empty;

        /// <summary>Meta approval status (APPROVED/PENDING/REJECTED/...). Preserved for debugging.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Meta category (UTILITY/MARKETING/AUTHENTICATION).</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// The template's components exactly as returned by Meta, so the frontend can
        /// inspect BODY/HEADER/BUTTON parameters and build the required input fields.
        /// </summary>
        public JsonElement? Components { get; set; }
    }

    /// <summary>
    /// Result of fetching templates from Meta. Token-free; carries a safe error on
    /// failure (never a stack trace or secret).
    /// </summary>
    public class GetTemplatesResult
    {
        public bool Success { get; set; }

        public List<WhatsAppTemplateDto> Templates { get; set; } = new();

        public string? Error { get; set; }
    }
}
