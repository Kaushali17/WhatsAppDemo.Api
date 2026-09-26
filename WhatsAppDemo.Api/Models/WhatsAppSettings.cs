namespace WhatsAppDemo.Api.Models
{
    public class WhatsAppSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = string.Empty;
        public string PhoneNumberId { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Token used to verify Meta's webhook subscription (GET challenge).
        /// </summary>
        public string VerifyToken { get; set; } = string.Empty;

        /// <summary>
        /// True when the Meta Cloud API credentials required to actually deliver
        /// a message are present.
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(PhoneNumberId) &&
            !string.IsNullOrWhiteSpace(AccessToken);
    }
}
