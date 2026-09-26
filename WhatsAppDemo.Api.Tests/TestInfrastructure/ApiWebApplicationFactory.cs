using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppDemo.Api.Data;
using WhatsAppDemo.Api.Services;

namespace WhatsAppDemo.Api.Tests.TestInfrastructure
{
    /// <summary>
    /// Boots WhatsAppDemo.Api in-memory for integration tests: replaces the SQL Server
    /// DbContext with an isolated EF Core InMemory database (unique per factory instance)
    /// and redirects the Meta WhatsApp HttpClient to a stub so no real network calls happen
    /// and WhatsAppCrmPoc is never touched.
    /// </summary>
    public class ApiWebApplicationFactory : WebApplicationFactory<Program>
    {
        public StubHttpMessageHandler MetaHandler { get; } = new();

        private readonly string _dbName = $"WhatsAppTestDb_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                // Dummy, non-secret values so WhatsAppService.SendToMetaAsync doesn't short-circuit
                // on "not configured" — the actual HTTP call is intercepted by MetaHandler below.
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["WhatsApp:BaseUrl"] = "https://graph.facebook.test",
                    ["WhatsApp:ApiVersion"] = "v25.0",
                    ["WhatsApp:PhoneNumberId"] = "TEST_PHONE_NUMBER_ID",
                    ["WhatsApp:AccessToken"] = "TEST_DUMMY_TOKEN_DO_NOT_USE",
                });
            });

            builder.ConfigureServices(services =>
            {
                var dbContextOptions = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (dbContextOptions is not null)
                {
                    services.Remove(dbContextOptions);
                }

                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_dbName));

                // Re-registering the typed client appends a handler-builder action that runs
                // after the default one, so our stub becomes the effective primary handler.
                services.AddHttpClient<IWhatsAppService, WhatsAppService>()
                    .ConfigurePrimaryHttpMessageHandler(() => MetaHandler);
            });
        }

        /// <summary>Runs a query/action against the test database via a fresh DI scope.</summary>
        public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await action(db);
        }
    }
}
