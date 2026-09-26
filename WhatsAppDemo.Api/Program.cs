
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

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("FrontendDev", policy =>
                    policy.WithOrigins("http://localhost:5173")
                          .AllowAnyHeader()
                          .AllowAnyMethod());
            });

            var app = builder.Build();

            // Seed POC data at runtime (no migration required).
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await DbSeeder.SeedAsync(db);
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // Serve uploaded media (images/documents) from wwwroot so they can be
            // opened/previewed directly via their MediaUrl.
            app.UseStaticFiles();

            app.UseCors("FrontendDev");

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
