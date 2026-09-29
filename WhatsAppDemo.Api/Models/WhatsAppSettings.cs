namespace WhatsAppDemo.Api.Models
{
    public class WhatsAppSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = string.Empty;
        public string PhoneNumberId { get; set; } = string.Empty;

        /// <summary>
        /// WhatsApp Business Account (WABA) id. Required to query the real approved
        /// message templates that live in Meta WhatsApp Manager.
        /// </summary>
        public string WabaId { get; set; } = string.Empty;

        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Token used to verify Meta's webhook subscription (GET challenge).
        /// </summary>
        public string VerifyToken { get; set; } = string.Empty;

        /// <summary>
        /// Token used to verify Meta's Cloud API webhook subscription (GET challenge).
        /// Configured via "WhatsApp:WebhookVerifyToken" and read from an Azure App
        /// Service application setting (WhatsApp__WebhookVerifyToken).
        /// Falls back to <see cref="VerifyToken"/> for backward compatibility.
        /// </summary>
        public string WebhookVerifyToken { get; set; } = string.Empty;

        /// <summary>
        /// Resolves the effective webhook verify token, preferring the new
        /// <see cref="WebhookVerifyToken"/> key and falling back to <see cref="VerifyToken"/>.
        /// </summary>
        public string EffectiveWebhookVerifyToken =>
            !string.IsNullOrWhiteSpace(WebhookVerifyToken) ? WebhookVerifyToken : VerifyToken;

        /// <summary>
        /// True when the Meta Cloud API credentials required to actually deliver
        /// a message are present.
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(PhoneNumberId) &&
            !string.IsNullOrWhiteSpace(AccessToken);
    }
}
