using WhatsAppDemo.Api.Dtos;

namespace WhatsAppDemo.Api.Services
{
    /// <summary>
    /// Mock WhatsApp provider used for the POC. It only simulates delivery and
    /// generates a fake provider message id. It must NOT depend on IWhatsAppProvider.
    /// </summary>
    public class MockWhatsAppProvider : IWhatsAppProvider
    {
        private readonly ILogger<MockWhatsAppProvider> _logger;

        public MockWhatsAppProvider(ILogger<MockWhatsAppProvider> logger)
        {
            _logger = logger;
        }

        public Task<WhatsAppProviderResult> SendMessageAsync(
            string phoneNumber,
            string? message,
            string messageType = "Text",
            string? mediaUrl = null)
        {
            _logger.LogInformation(
                "MOCK WHATSAPP -> To: {PhoneNumber} | Type: {MessageType} | Text: {Message} | Media: {MediaUrl}",
                phoneNumber, messageType, message, mediaUrl);

            var result = new WhatsAppProviderResult
            {
                Success = true,
                ProviderMessageId = $"mock-{Guid.NewGuid():N}"
            };

            return Task.FromResult(result);
        }
    }
}
