using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Tests.TestInfrastructure;

namespace WhatsAppDemo.Api.Tests
{
    public class SalesUsersControllerTests : IClassFixture<ApiWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public SalesUsersControllerTests(ApiWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetAll_ReturnsSeededUsers_Ok()
        {
            var response = await _client.GetAsync("/api/salesusers");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var users = await response.Content.ReadFromJsonAsync<List<SalesUserDto>>();
            users.Should().NotBeNull();
            users!.Count.Should().BeGreaterThanOrEqualTo(5);
        }

        [Fact]
        public async Task Create_ValidRequest_Returns201WithId()
        {
            var response = await _client.PostAsJsonAsync("/api/salesusers", new
            {
                name = "Test User",
                email = $"test.{Guid.NewGuid():N}@example.com"
            });

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = await response.Content.ReadFromJsonAsync<SalesUserDto>();
            created.Should().NotBeNull();
            created!.Id.Should().NotBeEmpty();
        }

        [Theory]
        [InlineData("not-an-email")]
        [InlineData("")]
        public async Task Create_InvalidEmail_Returns400(string email)
        {
            var response = await _client.PostAsJsonAsync("/api/salesusers", new
            {
                name = "Test User",
                email
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Create_MissingName_Returns400()
        {
            var response = await _client.PostAsJsonAsync("/api/salesusers", new
            {
                name = "",
                email = "valid@example.com"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
