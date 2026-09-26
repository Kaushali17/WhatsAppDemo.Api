using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WhatsAppDemo.Api.Tests.TestInfrastructure;

namespace WhatsAppDemo.Api.Tests
{
    /// <summary>
    /// Cross-cutting checks that the access token never appears anywhere a client could see it.
    /// </summary>
    public class SecurityTests : IClassFixture<ApiWebApplicationFactory>
    {
        private const string DummyAccessToken = "TEST_DUMMY_TOKEN_DO_NOT_USE";

        private readonly ApiWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public SecurityTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task LiveEndpoints_NeverLeakAccessTokenInResponseBody_OnSuccessOrFailure()
        {
            _factory.MetaHandler.EnqueueResponse(
                HttpStatusCode.OK, """{ "messages": [ { "id": "wamid.SECURITY_OK" } ] }""");
            var successResponse = await _client.PostAsJsonAsync("/api/whatsapp/send-text", new
            {
                recipientPhoneNumber = "917111111111",
                message = "security check"
            });
            var successBody = await successResponse.Content.ReadAsStringAsync();
            successBody.Should().NotContain(DummyAccessToken);

            _factory.MetaHandler.EnqueueResponse(
                HttpStatusCode.Unauthorized, """{ "error": { "message": "Session expired" } }""");
            var failureResponse = await _client.PostAsJsonAsync("/api/whatsapp/send-text", new
            {
                recipientPhoneNumber = "917111111112",
                message = "security check failure"
            });
            var failureBody = await failureResponse.Content.ReadAsStringAsync();
            failureBody.Should().NotContain(DummyAccessToken);
        }

        [Fact]
        public async Task CrmEndpoints_DoNotLeakAccessTokenAnywhereInResponses()
        {
            var customersBody = await (await _client.GetAsync("/api/customers")).Content.ReadAsStringAsync();
            var salesUsersBody = await (await _client.GetAsync("/api/salesusers")).Content.ReadAsStringAsync();
            var conversationsBody = await (await _client.GetAsync("/api/conversations")).Content.ReadAsStringAsync();

            customersBody.Should().NotContain(DummyAccessToken);
            salesUsersBody.Should().NotContain(DummyAccessToken);
            conversationsBody.Should().NotContain(DummyAccessToken);
        }
    }
}
