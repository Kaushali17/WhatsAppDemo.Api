using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Models;
using WhatsAppDemo.Api.Tests.TestInfrastructure;

namespace WhatsAppDemo.Api.Tests
{
    public class WhatsAppLiveControllerTests : IClassFixture<ApiWebApplicationFactory>
    {
        private const string MetaSuccessBody = """{ "messages": [ { "id": "wamid.TEST123" } ] }""";
        private const string MetaErrorBody = """{ "error": { "message": "Invalid parameter", "code": 100 } }""";

        private readonly ApiWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public WhatsAppLiveControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task SendTemplate_Valid_Returns200WithMessageId()
        {
            _factory.MetaHandler.EnqueueResponse(HttpStatusCode.OK, MetaSuccessBody);

            var response = await _client.PostAsJsonAsync("/api/whatsapp/send-template", new
            {
                recipientPhoneNumber = "917000000000",
                templateName = "jaspers_market_order_confirmation_v1",
                languageCode = "en_US"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<WhatsAppSendMessageResponse>();
            result!.Success.Should().BeTrue();
            result.MessageId.Should().Be("wamid.TEST123");
        }

        [Theory]
        [InlineData("", "jaspers_market_order_confirmation_v1", "en_US")]
        [InlineData("917000000000", "", "en_US")]
        [InlineData("917000000000", "jaspers_market_order_confirmation_v1", "")]
        public async Task SendTemplate_MissingRequiredField_Returns400(
            string recipientPhoneNumber, string templateName, string languageCode)
        {
            var response = await _client.PostAsJsonAsync("/api/whatsapp/send-template", new
            {
                recipientPhoneNumber,
                templateName,
                languageCode
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SendText_Valid_Returns200WithMessageId()
        {
            _factory.MetaHandler.EnqueueResponse(HttpStatusCode.OK, MetaSuccessBody);

            var response = await _client.PostAsJsonAsync("/api/whatsapp/send-text", new
            {
                recipientPhoneNumber = "917000000001",
                message = "Hello from tests"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<WhatsAppSendMessageResponse>();
            result!.Success.Should().BeTrue();
            result.MessageId.Should().Be("wamid.TEST123");
        }

        [Theory]
        [InlineData("", "some message")]
        [InlineData("917000000001", "")]
        public async Task SendText_MissingRequiredField_Returns400(string recipientPhoneNumber, string message)
        {
            var response = await _client.PostAsJsonAsync("/api/whatsapp/send-text", new
            {
                recipientPhoneNumber,
                message
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SendText_MetaReturnsError_MapsToBadRequestWithSafeErrorMessage()
        {
            _factory.MetaHandler.EnqueueResponse(HttpStatusCode.BadRequest, MetaErrorBody);

            var response = await _client.PostAsJsonAsync("/api/whatsapp/send-text", new
            {
                recipientPhoneNumber = "917000000002",
                message = "This will fail upstream"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var body = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<WhatsAppSendMessageResponse>(
                body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            result!.Success.Should().BeFalse();
            result.Error.Should().Contain("Invalid parameter");
            body.Should().NotContain("TEST_DUMMY_TOKEN_DO_NOT_USE");
        }

        [Fact]
        public async Task SendTemplate_BuildsCorrectMetaRequest_UrlAuthAndBody()
        {
            _factory.MetaHandler.EnqueueResponse(HttpStatusCode.OK, MetaSuccessBody);

            await _client.PostAsJsonAsync("/api/whatsapp/send-template", new
            {
                recipientPhoneNumber = "917000000003",
                templateName = "jaspers_market_order_confirmation_v1",
                languageCode = "en_US"
            });

            _factory.MetaHandler.LastRequestUri.Should().NotBeNull();
            _factory.MetaHandler.LastRequestUri!.ToString()
                .Should().Be("https://graph.facebook.test/v25.0/TEST_PHONE_NUMBER_ID/messages");
            _factory.MetaHandler.LastMethod.Should().Be(HttpMethod.Post);
            _factory.MetaHandler.LastAuthorizationHeader.Should().Be("Bearer TEST_DUMMY_TOKEN_DO_NOT_USE");

            _factory.MetaHandler.LastRequestBody.Should().NotBeNull();
            using var doc = JsonDocument.Parse(_factory.MetaHandler.LastRequestBody!);
            doc.RootElement.GetProperty("to").GetString().Should().Be("917000000003");
            doc.RootElement.GetProperty("type").GetString().Should().Be("template");
            doc.RootElement.GetProperty("template").GetProperty("name").GetString()
                .Should().Be("jaspers_market_order_confirmation_v1");
            doc.RootElement.GetProperty("template").GetProperty("language").GetProperty("code").GetString()
                .Should().Be("en_US");
        }

        [Theory]
        [InlineData(typeof(SendTemplateMessageRequest))]
        [InlineData(typeof(SendTextMessageRequest))]
        public void LiveRequestDtos_DoNotExposeAnAccessTokenProperty(Type requestType)
        {
            var propertyNames = requestType.GetProperties().Select(p => p.Name.ToLowerInvariant());
            propertyNames.Should().NotContain(name => name.Contains("token"));
        }

        [Fact]
        public async Task SendText_ExtraAccessTokenFieldInBody_IsIgnoredAndNeverForwarded()
        {
            _factory.MetaHandler.EnqueueResponse(HttpStatusCode.OK, MetaSuccessBody);

            var response = await _client.PostAsJsonAsync("/api/whatsapp/send-text", new
            {
                recipientPhoneNumber = "917000000004",
                message = "Attempting to smuggle a token",
                accessToken = "SHOULD_BE_IGNORED"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            _factory.MetaHandler.LastRequestBody.Should().NotContain("SHOULD_BE_IGNORED");
        }
    }
}
