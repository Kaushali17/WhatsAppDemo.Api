using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Models;

namespace WhatsAppDemo.Api.Services
{
    public interface IWhatsAppService
    {
        /// <summary>
        /// Sends an outgoing WhatsApp message via the configured provider and
        /// persists it against the customer's open conversation.
        /// </summary>
        Task<WhatsAppMessageResult> SendMessageAsync(SendWhatsAppMessageRequest request);

        /// <summary>
        /// Handles an incoming (mock) WhatsApp message, creating the customer
        /// and/or conversation as required, then persisting the message.
        /// </summary>
        Task<WhatsAppMessageResult> ReceiveMessageAsync(ReceiveWhatsAppMessageRequest request);


        /// <summary>
        /// Sends an approved WhatsApp template message through the Meta Cloud API.
        /// </summary>
        Task<WhatsAppSendMessageResponse> SendTemplateMessageAsync(
            string recipientPhoneNumber,
            string templateName,
            string languageCode,
            List<string>? parameters = null);

        /// <summary>
        /// Sends a free-form WhatsApp text message through the Meta Cloud API.
        /// </summary>
        Task<WhatsAppSendMessageResponse> SendTextMessageAsync(
            string recipientPhoneNumber,
            string message);
    }
}
