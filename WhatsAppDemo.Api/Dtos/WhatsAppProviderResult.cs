namespace WhatsAppDemo.Api.Dtos
{
    /// <summary>
    /// Result returned by an <c>IWhatsAppProvider</c> after attempting to deliver a message.
    /// </summary>
    public class WhatsAppProviderResult
    {
        public bool Success { get; set; }

        /// <summary>
        /// The provider-generated message id (e.g. Meta's wamid). Mocked for the POC.
        /// </summary>
        public string? ProviderMessageId { get; set; }

        public string? Error { get; set; }
    }
}
