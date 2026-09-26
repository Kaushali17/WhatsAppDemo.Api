using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Tests.TestInfrastructure;

namespace WhatsAppDemo.Api.Tests
{
    public class CustomersControllerTests : IClassFixture<ApiWebApplicationFactory>
    {
        private readonly ApiWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public CustomersControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetAll_ReturnsSeededCustomers_Ok()
        {
            var response = await _client.GetAsync("/api/customers");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var customers = await response.Content.ReadFromJsonAsync<List<CustomerDto>>();
            customers.Should().NotBeNull();
            customers!.Count.Should().BeGreaterThanOrEqualTo(6);
        }

        [Fact]
        public async Task GetById_ExistingCustomer_ReturnsOk()
        {
            var existingId = await _factory.WithDbAsync(db => db.Customers.Select(c => c.Id).FirstAsync());

            var response = await _client.GetAsync($"/api/customers/{existingId}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var dto = await response.Content.ReadFromJsonAsync<CustomerDto>();
            dto!.Id.Should().Be(existingId);
        }

        [Fact]
        public async Task GetById_RandomGuid_Returns404()
        {
            var response = await _client.GetAsync($"/api/customers/{Guid.NewGuid()}");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Create_ValidRequest_Returns201()
        {
            var response = await _client.PostAsJsonAsync("/api/customers", new
            {
                name = "New Co",
                whatsAppNumber = $"9199{Random.Shared.Next(1000000, 9999999)}"
            });

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var dto = await response.Content.ReadFromJsonAsync<CustomerDto>();
            dto!.Name.Should().Be("New Co");
        }

        [Fact]
        public async Task Create_MissingName_Returns400()
        {
            var response = await _client.PostAsJsonAsync("/api/customers", new
            {
                name = "",
                whatsAppNumber = "919999999999"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Create_NonExistentAssignedUserId_Returns400()
        {
            var response = await _client.PostAsJsonAsync("/api/customers", new
            {
                name = "Orphan Co",
                whatsAppNumber = "919999999998",
                assignedUserId = Guid.NewGuid()
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Update_ExistingCustomer_UpdatesFields()
        {
            var createResponse = await _client.PostAsJsonAsync("/api/customers", new
            {
                name = "Before Update",
                whatsAppNumber = $"9198{Random.Shared.Next(1000000, 9999999)}"
            });
            var created = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();

            var updateResponse = await _client.PutAsJsonAsync($"/api/customers/{created!.Id}", new
            {
                name = "After Update",
                whatsAppNumber = $"9197{Random.Shared.Next(1000000, 9999999)}"
            });

            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var updated = await updateResponse.Content.ReadFromJsonAsync<CustomerDto>();
            updated!.Name.Should().Be("After Update");
        }

        [Fact]
        public async Task Update_NonExistentCustomer_Returns404()
        {
            var response = await _client.PutAsJsonAsync($"/api/customers/{Guid.NewGuid()}", new
            {
                name = "Nobody",
                whatsAppNumber = "919999900003"
            });

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
