namespace WhatsAppDemo.Api.Services
{
    public interface IWhatsAppProvider
    {
        Task<bool> SendMessageAsync(
            string phoneNumber,
            string message);
    }
}
