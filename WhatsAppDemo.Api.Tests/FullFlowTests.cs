using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Tests.TestInfrastructure;

namespace WhatsAppDemo.Api.Tests
{
    /// <summary>
    /// End-to-end scenario exercising the full customer lifecycle across all mock/CRM endpoints.
    /// </summary>
    public class FullFlowTests : IClassFixture<ApiWebApplicationFactory>
    {
        private readonly ApiWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public FullFlowTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Webhook_List_Messages_Send_Assign_Close_VerifyFinalState()
        {
            var phone = $"91977{Random.Shared.Next(1000000, 9999999)}";

            // 1. Incoming webhook creates customer + conversation + incoming message.
            var webhookResponse = await _client.PostAsJsonAsync("/api/whatsapp/webhook", new
            {
                phoneNumber = phone,
                message = "Hi, I want to place an order",
                messageType = "Text",
                customerName = "Full Flow Customer"
            });
            webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var webhookResult = await webhookResponse.Content.ReadFromJsonAsync<WhatsAppMessageResult>();
            var conversationId = webhookResult!.Conversation!.Id;

            // 2. Conversation appears in the list.
            var listResponse = await _client.GetAsync("/api/conversations");
            var conversations = await listResponse.Content.ReadFromJsonAsync<List<ConversationDto>>();
            conversations.Should().Contain(c => c.Id == conversationId);

            // 3. Message thread contains the incoming message.
            var messagesResponse = await _client.GetAsync($"/api/conversations/{conversationId}/messages");
            var messages = await messagesResponse.Content.ReadFromJsonAsync<List<ChatMessageDto>>();
            messages.Should().ContainSingle(m => m.Direction == "Incoming");

            // 4. Outgoing reply via mock send.
            var sendResponse = await _client.PostAsJsonAsync("/api/whatsapp/send", new
            {
                phoneNumber = phone,
                message = "Sure, let me help with that",
                messageType = "Text"
            });
            sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 5. Assign to a sales user.
            var salesUserId = await _factory.WithDbAsync(db => db.SalesUsers.Select(u => u.Id).FirstAsync());
            var assignResponse = await _client.PutAsync(
                $"/api/conversations/{conversationId}/assign/{salesUserId}", null);
            assignResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var assigned = await assignResponse.Content.ReadFromJsonAsync<ConversationDto>();
            assigned!.AssignedUserId.Should().Be(salesUserId);

            // 6. Close the conversation.
            var closeResponse = await _client.PutAsync($"/api/conversations/{conversationId}/close", null);
            closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 7. Verify final state.
            var finalResponse = await _client.GetAsync($"/api/conversations/{conversationId}");
            var final = await finalResponse.Content.ReadFromJsonAsync<ConversationDto>();
            final!.Status.Should().Be("Closed");
            final.AssignedUserId.Should().Be(salesUserId);

            var finalMessagesResponse = await _client.GetAsync($"/api/conversations/{conversationId}/messages");
            var finalMessages = await finalMessagesResponse.Content.ReadFromJsonAsync<List<ChatMessageDto>>();
            finalMessages.Should().HaveCount(2);
            finalMessages.Should().BeInAscendingOrder(m => m.CreatedAt);
        }
    }
}
