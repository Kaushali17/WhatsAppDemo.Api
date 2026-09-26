using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Tests.TestInfrastructure;

namespace WhatsAppDemo.Api.Tests
{
    public class ConversationsControllerTests : IClassFixture<ApiWebApplicationFactory>
    {
        private readonly ApiWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public ConversationsControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<(Guid ConversationId, Guid CustomerId, string Phone)> CreateConversationViaWebhookAsync(string prefix)
        {
            var phone = $"{prefix}{Random.Shared.Next(1000000, 9999999)}";

            var response = await _client.PostAsJsonAsync("/api/whatsapp/webhook", new
            {
                phoneNumber = phone,
                message = "Hi, need help",
                messageType = "Text",
                customerName = $"Test Customer {prefix}"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<WhatsAppMessageResult>();
            result!.Conversation.Should().NotBeNull();

            return (result.Conversation!.Id, result.Conversation.CustomerId, phone);
        }

        [Fact]
        public async Task GetAll_ReturnsConversations_OrderedByLastMessageAtDescending()
        {
            await CreateConversationViaWebhookAsync("92001");
            await CreateConversationViaWebhookAsync("92002");

            var response = await _client.GetAsync("/api/conversations");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var conversations = await response.Content.ReadFromJsonAsync<List<ConversationDto>>();
            conversations.Should().NotBeNull();
            conversations!.Count.Should().BeGreaterThanOrEqualTo(2);
            conversations.Should().BeInDescendingOrder(c => c.LastMessageAt);
        }

        [Fact]
        public async Task GetById_ExistingId_ReturnsOk()
        {
            var (conversationId, _, _) = await CreateConversationViaWebhookAsync("92010");

            var response = await _client.GetAsync($"/api/conversations/{conversationId}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var dto = await response.Content.ReadFromJsonAsync<ConversationDto>();
            dto!.Id.Should().Be(conversationId);
        }

        [Fact]
        public async Task GetById_UnknownId_Returns404()
        {
            var response = await _client.GetAsync($"/api/conversations/{Guid.NewGuid()}");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetByCustomer_ExistingCustomer_ReturnsOk()
        {
            var (_, customerId, _) = await CreateConversationViaWebhookAsync("92020");

            var response = await _client.GetAsync($"/api/conversations/customer/{customerId}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var conversations = await response.Content.ReadFromJsonAsync<List<ConversationDto>>();
            conversations!.Should().ContainSingle(c => c.CustomerId == customerId);
        }

        [Fact]
        public async Task GetByCustomer_UnknownCustomer_Returns404()
        {
            var response = await _client.GetAsync($"/api/conversations/customer/{Guid.NewGuid()}");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Assign_ValidSalesUser_SetsAssignedUserId()
        {
            var (conversationId, _, _) = await CreateConversationViaWebhookAsync("92030");
            var salesUserId = await _factory.WithDbAsync(db => db.SalesUsers.Select(u => u.Id).FirstAsync());

            var response = await _client.PutAsync($"/api/conversations/{conversationId}/assign/{salesUserId}", null);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var dto = await response.Content.ReadFromJsonAsync<ConversationDto>();
            dto!.AssignedUserId.Should().Be(salesUserId);
        }

        [Fact]
        public async Task Assign_UnknownSalesUser_Returns404()
        {
            var (conversationId, _, _) = await CreateConversationViaWebhookAsync("92031");

            var response = await _client.PutAsync($"/api/conversations/{conversationId}/assign/{Guid.NewGuid()}", null);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Assign_UnknownConversation_Returns404()
        {
            var salesUserId = await _factory.WithDbAsync(db => db.SalesUsers.Select(u => u.Id).FirstAsync());

            var response = await _client.PutAsync($"/api/conversations/{Guid.NewGuid()}/assign/{salesUserId}", null);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Close_ExistingConversation_SetsStatusClosed()
        {
            var (conversationId, _, _) = await CreateConversationViaWebhookAsync("92040");

            var response = await _client.PutAsync($"/api/conversations/{conversationId}/close", null);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var dto = await response.Content.ReadFromJsonAsync<ConversationDto>();
            dto!.Status.Should().Be("Closed");
        }

        [Fact]
        public async Task GetMessages_ExistingConversation_OrderedOldestToNewest()
        {
            var (conversationId, _, phone) = await CreateConversationViaWebhookAsync("92050");

            await _client.PostAsJsonAsync("/api/whatsapp/webhook", new
            {
                phoneNumber = phone,
                message = "follow up",
                messageType = "Text"
            });

            var response = await _client.GetAsync($"/api/conversations/{conversationId}/messages");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var messages = await response.Content.ReadFromJsonAsync<List<ChatMessageDto>>();
            messages.Should().NotBeNullOrEmpty();
            messages!.Count.Should().BeGreaterThanOrEqualTo(2);
            messages.Should().BeInAscendingOrder(m => m.CreatedAt);
        }

        [Fact]
        public async Task GetMessages_UnknownConversation_Returns404()
        {
            var response = await _client.GetAsync($"/api/conversations/{Guid.NewGuid()}/messages");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
