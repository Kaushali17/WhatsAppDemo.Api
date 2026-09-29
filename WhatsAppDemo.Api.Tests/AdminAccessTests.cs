using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Models;
using WhatsAppDemo.Api.Tests.TestInfrastructure;

namespace WhatsAppDemo.Api.Tests
{
    /// <summary>
    /// Verifies role-based CRM data access: an Admin (Amit) can see all salespeople's
    /// conversations/customers, while a Salesperson can only see their own. Sending
    /// still routes through the sender's own WhatsApp Business number regardless of role.
    /// </summary>
    public class AdminAccessTests : IClassFixture<ApiWebApplicationFactory>
    {
        private const string AdminPhoneNumberId = "ADMIN_PN_ID";
        private const string SalesAPhoneNumberId = "SALES_A_PN_ID";
        private const string SalesBPhoneNumberId = "SALES_B_PN_ID";

        private const string MetaSuccessBody = """{ "messages": [ { "id": "wamid.ADMINTEST" } ] }""";

        private readonly ApiWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public AdminAccessTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private static string NewPhone(string prefix) => $"{prefix}{Random.Shared.Next(1000000, 9999999)}";

        private static readonly Guid AdminUserId = Guid.Parse("C5DC5BCC-524C-4391-BC0E-513A4A24B700");

        /// <summary>
        /// Ensures the Admin (Amit) plus two salespeople each with their own business
        /// number exist. Returns their sales user ids.
        /// </summary>
        private async Task<(Guid adminId, Guid salesAId, Guid salesBId)> EnsureUsersAsync()
        {
            return await _factory.WithDbAsync(async db =>
            {
                async Task<Guid> EnsureUserAsync(Guid? id, string name, string email, string role, string phoneNumberId, string display)
                {
                    var user = id is Guid uid
                        ? await db.SalesUsers.FirstOrDefaultAsync(u => u.Id == uid)
                        : await db.SalesUsers.FirstOrDefaultAsync(u => u.Email == email);
                    if (user is null)
                    {
                        user = new SalesUser
                        {
                            Id = id ?? Guid.NewGuid(),
                            Name = name,
                            Email = email,
                            Role = role
                        };
                        db.SalesUsers.Add(user);
                    }
                    else if (user.Role != role)
                    {
                        user.Role = role;
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

                var admin = await EnsureUserAsync(AdminUserId, "Amit", "amit-admin@crm.local", "Admin", AdminPhoneNumberId, "+91 90000 00000");
                var salesA = await EnsureUserAsync(null, "Sales A", "sales.a@crm.local", "Salesperson", SalesAPhoneNumberId, "+91 91111 11111");
                var salesB = await EnsureUserAsync(null, "Sales B", "sales.b@crm.local", "Salesperson", SalesBPhoneNumberId, "+91 92222 22222");
                return (admin, salesA, salesB);
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

        private async Task<Guid> CreateConversationAsync(string phoneNumberId, string fromPhone, string text)
        {
            var response = await _client.PostAsJsonAsync(
                "/api/whatsapp/webhook",
                BuildMetaWebhook(phoneNumberId, fromPhone, text));
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            return await _factory.WithDbAsync(db =>
                db.Conversations
                    .Where(c => c.Customer!.WhatsAppNumber == fromPhone)
                    .Select(c => c.Id)
                    .FirstAsync());
        }

        // Test 1: Admin requests conversations -> all salespeople's conversations returned.
        [Fact]
        public async Task Admin_GetAll_ReturnsAllSalespeopleConversations()
        {
            var (adminId, salesAId, salesBId) = await EnsureUsersAsync();

            var convA = await CreateConversationAsync(SalesAPhoneNumberId, NewPhone("93101"), "From A's customer");
            var convB = await CreateConversationAsync(SalesBPhoneNumberId, NewPhone("93102"), "From B's customer");

            var response = await _client.GetAsync($"/api/conversations?salesUserId={adminId}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var conversations = await response.Content.ReadFromJsonAsync<List<ConversationDto>>();
            conversations!.Select(c => c.Id).Should().Contain(new[] { convA, convB });
        }

        // Test 2: Salesperson A requests conversations -> only A's conversations returned.
        [Fact]
        public async Task SalespersonA_GetAll_ReturnsOnlyOwnConversations()
        {
            var (_, salesAId, salesBId) = await EnsureUsersAsync();

            var convA = await CreateConversationAsync(SalesAPhoneNumberId, NewPhone("93111"), "From A's customer");
            var convB = await CreateConversationAsync(SalesBPhoneNumberId, NewPhone("93112"), "From B's customer");

            var response = await _client.GetAsync($"/api/conversations?salesUserId={salesAId}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var conversations = await response.Content.ReadFromJsonAsync<List<ConversationDto>>();
            conversations!.Should().OnlyContain(c => c.AssignedUserId == salesAId);
            conversations!.Select(c => c.Id).Should().Contain(convA).And.NotContain(convB);
        }

        // Test 3: Salesperson B requests conversations -> only B's conversations returned.
        [Fact]
        public async Task SalespersonB_GetAll_ReturnsOnlyOwnConversations()
        {
            var (_, salesAId, salesBId) = await EnsureUsersAsync();

            var convA = await CreateConversationAsync(SalesAPhoneNumberId, NewPhone("93121"), "From A's customer");
            var convB = await CreateConversationAsync(SalesBPhoneNumberId, NewPhone("93122"), "From B's customer");

            var response = await _client.GetAsync($"/api/conversations?salesUserId={salesBId}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var conversations = await response.Content.ReadFromJsonAsync<List<ConversationDto>>();
            conversations!.Should().OnlyContain(c => c.AssignedUserId == salesBId);
            conversations!.Select(c => c.Id).Should().Contain(convB).And.NotContain(convA);
        }

        // Test 4 & 5: Incoming on Salesperson A's number assigns to A; Admin can see it.
        [Fact]
        public async Task Incoming_ForSalespersonA_IsVisibleToAdmin()
        {
            var (adminId, salesAId, _) = await EnsureUsersAsync();
            var fromPhone = NewPhone("93131");

            var convA = await CreateConversationAsync(SalesAPhoneNumberId, fromPhone, "Hi A");

            var assignedUserId = await _factory.WithDbAsync(db =>
                db.Conversations.Where(c => c.Id == convA).Select(c => c.AssignedUserId).FirstAsync());
            assignedUserId.Should().Be(salesAId);

            var response = await _client.GetAsync($"/api/conversations/{convA}?salesUserId={adminId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var dto = await response.Content.ReadFromJsonAsync<ConversationDto>();
            dto!.Id.Should().Be(convA);
        }

        // Test 6: Salesperson B attempts to open Salesperson A's conversation -> denied.
        [Fact]
        public async Task SalespersonB_CannotAccessSalespersonAConversation()
        {
            var (_, _, salesBId) = await EnsureUsersAsync();
            var convA = await CreateConversationAsync(SalesAPhoneNumberId, NewPhone("93141"), "Hi A");

            var getResponse = await _client.GetAsync($"/api/conversations/{convA}?salesUserId={salesBId}");
            getResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            var msgResponse = await _client.GetAsync($"/api/conversations/{convA}/messages?salesUserId={salesBId}");
            msgResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // Test 7: Admin sends a message on an Admin-owned conversation -> Admin's number used.
        [Fact]
        public async Task Admin_SendsMessage_UsesAdminBusinessNumber()
        {
            var (adminId, _, _) = await EnsureUsersAsync();
            var fromPhone = NewPhone("93151");

            var convAdmin = await CreateConversationAsync(AdminPhoneNumberId, fromPhone, "Incoming to Admin");

            _factory.MetaHandler.EnqueueResponse(HttpStatusCode.OK, MetaSuccessBody);

            var response = await _client.PostAsJsonAsync("/api/whatsapp/send", new
            {
                conversationId = convAdmin,
                message = "Reply from Admin",
                messageType = "Text"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            _factory.MetaHandler.LastRequestUri!.ToString()
                .Should().Contain($"/{AdminPhoneNumberId}/messages");
        }

        // Test 8: Salesperson A sends a message -> Salesperson A's number used.
        [Fact]
        public async Task SalespersonA_SendsMessage_UsesSalespersonABusinessNumber()
        {
            await EnsureUsersAsync();
            var fromPhone = NewPhone("93161");

            var convA = await CreateConversationAsync(SalesAPhoneNumberId, fromPhone, "Incoming to A");

            _factory.MetaHandler.EnqueueResponse(HttpStatusCode.OK, MetaSuccessBody);

            var response = await _client.PostAsJsonAsync("/api/whatsapp/send", new
            {
                conversationId = convA,
                message = "Reply from A",
                messageType = "Text"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            _factory.MetaHandler.LastRequestUri!.ToString()
                .Should().Contain($"/{SalesAPhoneNumberId}/messages");
        }
    }
}
