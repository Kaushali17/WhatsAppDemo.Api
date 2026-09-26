using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Tests.TestInfrastructure;

namespace WhatsAppDemo.Api.Tests
{
    public class WhatsAppMockControllerTests : IClassFixture<ApiWebApplicationFactory>
    {
        private readonly ApiWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public WhatsAppMockControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private static string NewPhone(string prefix) => $"{prefix}{Random.Shared.Next(1000000, 9999999)}";

        [Fact]
        public async Task Webhook_NewNumber_CreatesCustomerConversationAndIncomingMessage()
        {
            var phone = NewPhone("91999");

            var response = await _client.PostAsJsonAsync("/api/whatsapp/webhook", new
            {
                phoneNumber = phone,
                message = "Hello there",
                messageType = "Text",
                customerName = "Brand New Customer"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<WhatsAppMessageResult>();

            result!.Success.Should().BeTrue();
            result.Conversation.Should().NotBeNull();
            result.ChatMessage.Should().NotBeNull();
            result.ChatMessage!.Direction.Should().Be("Incoming");

            var customerExists = await _factory.WithDbAsync(
                db => db.Customers.AnyAsync(c => c.WhatsAppNumber == phone));
            customerExists.Should().BeTrue();
        }

        [Fact]
        public async Task Webhook_ExistingSeededNumber_ReusesCustomer_DoesNotCreateDuplicate()
        {
            var seededPhone = await _factory.WithDbAsync(db => db.Customers.Select(c => c.WhatsAppNumber).FirstAsync());
            var customerCountBefore = await _factory.WithDbAsync(db => db.Customers.CountAsync());

            var response = await _client.PostAsJsonAsync("/api/whatsapp/webhook", new
            {
                phoneNumber = seededPhone,
                message = "Reusing existing customer",
                messageType = "Text"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var customerCountAfter = await _factory.WithDbAsync(db => db.Customers.CountAsync());
            customerCountAfter.Should().Be(customerCountBefore);
        }

        [Fact]
        public async Task Send_ToExistingNumber_CreatesOutgoingMessageAndUpdatesLastMessageAt()
        {
            var phone = NewPhone("91998");
            await _client.PostAsJsonAsync("/api/whatsapp/webhook", new
            {
                phoneNumber = phone,
                message = "Incoming first",
                messageType = "Text"
            });

            var response = await _client.PostAsJsonAsync("/api/whatsapp/send", new
            {
                phoneNumber = phone,
                message = "Outgoing reply",
                messageType = "Text"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<WhatsAppMessageResult>();
            result!.Success.Should().BeTrue();
            result.ChatMessage!.Direction.Should().Be("Outgoing");
        }

        [Fact]
        public async Task Send_UnknownNumber_ReturnsFailureResult()
        {
            var response = await _client.PostAsJsonAsync("/api/whatsapp/send", new
            {
                phoneNumber = NewPhone("91000"),
                message = "Hello",
                messageType = "Text"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<WhatsAppMessageResult>();
            result!.Success.Should().BeFalse();
        }

        [Fact]
        public async Task Send_MissingPhoneNumber_Returns400()
        {
            var response = await _client.PostAsJsonAsync("/api/whatsapp/send", new
            {
                message = "Hello",
                messageType = "Text"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Send_TextTypeMissingMessage_Returns400()
        {
            var response = await _client.PostAsJsonAsync("/api/whatsapp/send", new
            {
                phoneNumber = "919999999997",
                messageType = "Text"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Theory]
        [InlineData("Image")]
        [InlineData("Document")]
        public async Task Send_MediaTypeMissingMediaUrl_Returns400(string messageType)
        {
            var response = await _client.PostAsJsonAsync("/api/whatsapp/send", new
            {
                phoneNumber = "919999999996",
                messageType
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
