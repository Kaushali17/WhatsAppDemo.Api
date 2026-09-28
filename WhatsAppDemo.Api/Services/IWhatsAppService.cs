using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Models;

namespace WhatsAppDemo.Api.Services
{
    public interface IWhatsAppService
    {
        /// <summary>
        /// Retrieves the real WhatsApp message templates from the configured Meta WABA
        /// (/message_templates). The access token is resolved server-side and never
        /// returned to the caller. When <paramref name="approvedOnly"/> is true, only
        /// APPROVED templates are returned.
        /// </summary>
        Task<Dtos.GetTemplatesResult> GetTemplatesAsync(bool approvedOnly = true);

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
        /// Updates the delivery status ("sent"/"delivered"/"read"/"failed") of a
        /// previously sent outgoing message, matched by its Meta message id. No-op when
        /// the message id is unknown. Never throws for unknown ids.
        /// </summary>
        Task UpdateMessageStatusAsync(string whatsAppMessageId, string status);

        /// <summary>
        /// Sends an approved WhatsApp template message through the Meta Cloud API.
        /// </summary>
        Task<WhatsAppSendMessageResponse> SendTemplateMessageAsync(
            string recipientPhoneNumber,
            string templateName,
            string languageCode,
            List<string>? parameters = null,
            string? senderPhoneNumberId = null,
            string? senderAccessToken = null);

        /// <summary>
        /// Sends an approved WhatsApp template to an existing customer, creating or
        /// reusing the customer's open conversation and persisting the outgoing
        /// message. Nothing is persisted when Meta rejects the send.
        /// </summary>
        Task<SendTemplateMessageResponse> SendTemplateToCustomerAsync(
            SendTemplateToCustomerRequest request);

        /// <summary>
        /// Sends a free-form WhatsApp text message through the Meta Cloud API.
        /// </summary>
        Task<WhatsAppSendMessageResponse> SendTextMessageAsync(
            string recipientPhoneNumber,
            string message);
    }
}
