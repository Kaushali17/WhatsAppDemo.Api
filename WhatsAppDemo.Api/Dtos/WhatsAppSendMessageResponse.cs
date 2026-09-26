namespace WhatsAppDemo.Api.Dtos
{
    /// <summary>
    /// Clean application-level result returned after calling the Meta WhatsApp Cloud API.
    /// Never contains the access token or raw provider credentials.
    /// </summary>
    public class WhatsAppSendMessageResponse
    {
        public bool Success { get; set; }

        /// <summary>
        /// Meta message id (e.g. "wamid....") when the send succeeds.
        /// </summary>
        public string? MessageId { get; set; }

        /// <summary>
        /// A safe, human-readable error message when the send fails.
        /// </summary>
        public string? Error { get; set; }
    }
}
