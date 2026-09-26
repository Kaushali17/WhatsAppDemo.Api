using WhatsAppDemo.Api.Dtos;

namespace WhatsAppDemo.Api.Services
{
    /// <summary>
    /// Abstraction over the underlying WhatsApp delivery channel.
    /// The mock implementation can later be swapped for a real MetaWhatsAppProvider
    /// without touching the controller or CRM business logic.
    /// </summary>
    public interface IWhatsAppProvider
    {
        Task<WhatsAppProviderResult> SendMessageAsync(
            string phoneNumber,
            string? message,
            string messageType = "Text",
            string? mediaUrl = null);
    }
}
