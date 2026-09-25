namespace WhatsAppDemo.Api.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly IWhatsAppProvider _provider;

        public WhatsAppService(IWhatsAppProvider provider)
        {
            _provider = provider;
        }
        public async Task<bool> SendMessageAsync(
           string phoneNumber,
           string message)
        {
            return await _provider.SendMessageAsync(
                phoneNumber,
                message);
        }
    }
}
