using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WhatsAppDemo.Api.Models;
using WhatsAppDemo.Api.Tests.TestInfrastructure;

namespace WhatsAppDemo.Api.Tests
{
    /// <summary>
    /// Verifies that multiple WhatsApp Business numbers under the same WABA are routed
    /// correctly: incoming messages are attributed to the sales person that owns the
    /// receiving phone_number_id, unknown numbers are not assigned to anyone, and
    /// outgoing messages go out through the conversation's own phone_number_id.
    /// </summary>
    public class WhatsAppMultiNumberTests : IClassFixture<ApiWebApplicationFactory>
    {
        // The two real numbers described by the CRM requirements.
        private const string Salesperson1PhoneNumberId = "1367575223100221";
        private const string Salesperson2PhoneNumberId = "1287489887791761";

        private const string MetaSuccessBody = """{ "messages": [ { "id": "wamid.TEST123" } ] }""";

        private readonly ApiWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public WhatsAppMultiNumberTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private static string NewPhone(string prefix) => $"{prefix}{Random.Shared.Next(1000000, 9999999)}";

        /// <summary>
        /// Ensures both business numbers exist (each owned by its own sales person) and
        /// returns their sales user ids. Safe to call from multiple tests.
        /// </summary>
        private async Task<(Guid salesUser1Id, Guid salesUser2Id)> EnsureBusinessNumbersAsync()
        {
            return await _factory.WithDbAsync(async db =>
            {
                async Task<Guid> EnsureAsync(string name, string email, string phoneNumberId, string display)
                {
                    var user = await db.SalesUsers.FirstOrDefaultAsync(u => u.Email == email);
                    if (user is null)
                    {
                        user = new SalesUser { Id = Guid.NewGuid(), Name = name, Email = email };
                        db.SalesUsers.Add(user);
                    }

                    var number = await db.WhatsAppBusinessNumbers
                        .FirstOrDefaultAsync(n => n.PhoneNumberId == phoneNumberId);
                    if (number is null)
                    {
                        db.WhatsAppBusinessNumbers.Add(new WhatsAppBusinessNumber
                        {
                            Id = Guid.NewGuid(),
                            SalesUserId = user.Id,
                            PhoneNumberId = phoneNumberId,
                            DisplayPhoneNumber = display,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    await db.SaveChangesAsync();
                    return user.Id;
                }

                var s1 = await EnsureAsync("Salesperson 1", "sp1@crm.local", Salesperson1PhoneNumberId, "+91 97258 62006");
                var s2 = await EnsureAsync("Salesperson 2", "sp2@crm.local", Salesperson2PhoneNumberId, "+91 99095 58948");
                return (s1, s2);
            });
        }

        private static object BuildMetaWebhook(string phoneNumberId, string fromPhone, string text)
            => new
            {
                @object = "whatsapp_business_account",
                entry = new[]
                {
                    new
                    {
                        id = "WABA_ID",
                        changes = new[]
                        {
                            new
                            {
                                field = "messages",
                                value = new
                                {
                                    messaging_product = "whatsapp",
                                    metadata = new
                                    {
                                        display_phone_number = "0000",
                                        phone_number_id = phoneNumberId
                                    },
                                    contacts = new[]
                                    {
                                        new { profile = new { name = "Test Customer" }, wa_id = fromPhone }
                                    },
                                    messages = new[]
                                    {
                                        new
                                        {
                                            from = fromPhone,
                                            id = $"wamid.{Guid.NewGuid():N}",
                                            timestamp = "1700000000",
                                            type = "text",
                                            text = new { body = text }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

        [Fact]
        public async Task Incoming_ForSalesperson1Number_AssignsCustomerToSalesperson1()
        {
            var (salesUser1Id, _) = await EnsureBusinessNumbersAsync();
            var fromPhone = NewPhone("91901");

            var response = await _client.PostAsJsonAsync(
                "/api/whatsapp/webhook",
                BuildMetaWebhook(Salesperson1PhoneNumberId, fromPhone, "Hi Salesperson 1"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var customer = await _factory.WithDbAsync(db =>
                db.Customers.FirstOrDefaultAsync(c => c.WhatsAppNumber == fromPhone));
            customer.Should().NotBeNull();
            customer!.AssignedUserId.Should().Be(salesUser1Id);
        }

        [Fact]
        public async Task Incoming_ForSalesperson2Number_AssignsCustomerToSalesperson2()
        {
            var (_, salesUser2Id) = await EnsureBusinessNumbersAsync();
            var fromPhone = NewPhone("91902");

            var response = await _client.PostAsJsonAsync(
                "/api/whatsapp/webhook",
                BuildMetaWebhook(Salesperson2PhoneNumberId, fromPhone, "Hi Salesperson 2"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var customer = await _factory.WithDbAsync(db =>
                db.Customers.FirstOrDefaultAsync(c => c.WhatsAppNumber == fromPhone));
            customer.Should().NotBeNull();
            customer!.AssignedUserId.Should().Be(salesUser2Id);
        }

        [Fact]
        public async Task Incoming_ForUnknownPhoneNumberId_DoesNotAssignAnySalesperson()
        {
            await EnsureBusinessNumbersAsync();
            var fromPhone = NewPhone("91903");

            var response = await _client.PostAsJsonAsync(
                "/api/whatsapp/webhook",
                BuildMetaWebhook("UNKNOWN_PHONE_NUMBER_ID", fromPhone, "Hello?"));

            // The webhook endpoint must not crash on an unknown number.
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // The message must not be attributed to any sales person: no customer created.
            var customerExists = await _factory.WithDbAsync(db =>
                db.Customers.AnyAsync(c => c.WhatsAppNumber == fromPhone));
            customerExists.Should().BeFalse();
        }

        [Fact]
        public async Task Outgoing_ForSalesperson1Conversation_UsesSalesperson1PhoneNumberId()
        {
            await EnsureBusinessNumbersAsync();
            var fromPhone = NewPhone("91904");

            // Create the conversation via an incoming message on Salesperson 1's number.
            await _client.PostAsJsonAsync(
                "/api/whatsapp/webhook",
                BuildMetaWebhook(Salesperson1PhoneNumberId, fromPhone, "Incoming to SP1"));

            var conversationId = await _factory.WithDbAsync(db =>
                db.Conversations
                    .Where(c => c.Customer!.WhatsAppNumber == fromPhone)
                    .Select(c => c.Id)
                    .FirstAsync());

            _factory.MetaHandler.EnqueueResponse(HttpStatusCode.OK, MetaSuccessBody);

            var response = await _client.PostAsJsonAsync("/api/whatsapp/send", new
            {
                conversationId,
                message = "Reply from SP1",
                messageType = "Text"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            _factory.MetaHandler.LastRequestUri!.ToString()
                .Should().Contain($"/{Salesperson1PhoneNumberId}/messages");
        }

        [Fact]
        public async Task Outgoing_ForSalesperson2Conversation_UsesSalesperson2PhoneNumberId()
        {
            await EnsureBusinessNumbersAsync();
            var fromPhone = NewPhone("91905");

            await _client.PostAsJsonAsync(
                "/api/whatsapp/webhook",
                BuildMetaWebhook(Salesperson2PhoneNumberId, fromPhone, "Incoming to SP2"));

            var conversationId = await _factory.WithDbAsync(db =>
                db.Conversations
                    .Where(c => c.Customer!.WhatsAppNumber == fromPhone)
                    .Select(c => c.Id)
                    .FirstAsync());

            _factory.MetaHandler.EnqueueResponse(HttpStatusCode.OK, MetaSuccessBody);

            var response = await _client.PostAsJsonAsync("/api/whatsapp/send", new
            {
                conversationId,
                message = "Reply from SP2",
                messageType = "Text"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            _factory.MetaHandler.LastRequestUri!.ToString()
                .Should().Contain($"/{Salesperson2PhoneNumberId}/messages");
        }

        [Fact]
        public async Task Outgoing_LegacySingleNumber_StillUsesConfiguredPhoneNumberId()
        {
            // Backward-compatibility: a legacy send by customer phone number (no
            // conversation business number) falls back to the globally configured id.
            var phone = NewPhone("91906");
            await _client.PostAsJsonAsync("/api/whatsapp/webhook", new
            {
                phoneNumber = phone,
                message = "Legacy incoming",
                messageType = "Text"
            });

            _factory.MetaHandler.EnqueueResponse(HttpStatusCode.OK, MetaSuccessBody);

            var response = await _client.PostAsJsonAsync("/api/whatsapp/send", new
            {
                phoneNumber = phone,
                message = "Legacy reply",
                messageType = "Text"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            _factory.MetaHandler.LastRequestUri!.ToString()
                .Should().Contain("/TEST_PHONE_NUMBER_ID/messages");
        }
    }
}
