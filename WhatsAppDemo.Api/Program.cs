
using Microsoft.EntityFrameworkCore;
using WhatsAppDemo.Api.Data;
using WhatsAppDemo.Api.Models;
using WhatsAppDemo.Api.Services;

namespace WhatsAppDemo.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.Configure<WhatsAppSettings>(builder.Configuration.GetSection("WhatsApp"));

            // Registers IWhatsAppService -> WhatsAppService together with a typed HttpClient.
            // Do NOT add a second AddScoped registration; it would override this and break
            // HttpClient injection.
            builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>();

            builder.Services.AddScoped<IWhatsAppProvider, MockWhatsAppProvider>();

            builder.Services.AddScoped<IMediaStorageService, LocalMediaStorageService>();

            builder.Services.AddScoped<IUserAccessResolver, UserAccessResolver>();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("Frontend", policy =>
                {
                    policy.WithOrigins(
                            "http://localhost:5173",
                            "https://wonderful-plant-07b10fe10.1.azurestaticapps.net"
                        )
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            var app = builder.Build();

            // Safe startup validation of the Meta WhatsApp configuration. Logs which
            // required keys are present/missing WITHOUT ever printing their values.
            using (var scope = app.Services.CreateScope())
            {
                var settings = builder.Configuration.GetSection("WhatsApp").Get<WhatsAppSettings>()
                    ?? new WhatsAppSettings();
                var startupLogger = scope.ServiceProvider
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("WhatsAppConfig");

                bool has(string? v) => !string.IsNullOrWhiteSpace(v);

                startupLogger.LogInformation(
                    "WhatsApp config check -> AccessToken: {Token}, PhoneNumberId: {Phone}, WabaId: {Waba}.",
                    has(settings.AccessToken) ? "present" : "MISSING",
                    has(settings.PhoneNumberId) ? "present" : "MISSING",
                    has(settings.WabaId) ? "present" : "MISSING");

                if (!has(settings.AccessToken) || !has(settings.PhoneNumberId) || !has(settings.WabaId))
                {
                    startupLogger.LogWarning(
                        "One or more WhatsApp configuration values are missing. Real Meta " +
                        "template retrieval/sending will be unavailable until they are set.");
                }
            }

            // Apply pending EF Core migrations and seed POC data at runtime.
            // Wrapped in try/catch so a transient/misconfigured database does not
            // crash application startup (which surfaces as HTTP 500.30 on Azure).
            using (var scope = app.Services.CreateScope())
            {
                var seedLogger = scope.ServiceProvider
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("DbStartup");
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await db.Database.MigrateAsync();
                    await DbSeeder.SeedAsync(db, builder.Configuration);
                }
                catch (Exception ex)
                {
                    seedLogger.LogError(ex,
                        "Database migration/seeding failed at startup. The application " +
                        "will continue to start, but data endpoints may return errors " +
                        "until the database connection string, firewall, and schema are " +
                        "correctly configured.");
                }
            }

            // Configure the HTTP request pipeline.
            //if (app.Environment.IsDevelopment())
            //{
                app.UseSwagger();
                app.UseSwaggerUI();
            //}

            app.UseHttpsRedirection();

            // Serve uploaded media (images/documents) from wwwroot so they can be
            // opened/previewed directly via their MediaUrl.
            app.UseStaticFiles();

            app.UseCors("Frontend");

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
