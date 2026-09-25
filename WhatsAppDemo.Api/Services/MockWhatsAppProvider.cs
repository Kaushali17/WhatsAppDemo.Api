namespace WhatsAppDemo.Api.Services
{
    public class MockWhatsAppProvider : IWhatsAppProvider
    {
        public Task<bool> SendMessageAsync(
        string phoneNumber,
        string message)
        {
            Console.WriteLine(
                $"MOCK WHATSAPP → {phoneNumber}: {message}");

            return Task.FromResult(true);
        }
    }
}
