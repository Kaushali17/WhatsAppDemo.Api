namespace WhatsAppDemo.Api.Services
{
    public interface IWhatsAppService
    {
        Task<bool> SendMessageAsync(
           string phoneNumber,
           string message);
    }
}
