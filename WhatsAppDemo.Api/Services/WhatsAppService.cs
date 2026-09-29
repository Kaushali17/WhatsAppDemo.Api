using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime;
using System.Text;
using System.Text.Json;
using WhatsAppDemo.Api.Data;
using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Models;
using WhatsAppDemo.Api.Services;

namespace WhatsAppDemo.Api.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly IWhatsAppProvider _provider;
        private readonly AppDbContext _db;
        private readonly ILogger<WhatsAppService> _logger;
        private readonly HttpClient _httpClient;
        private readonly WhatsAppSettings _settings;
        private readonly IMediaStorageService _mediaStorage;

        public WhatsAppService(
            IWhatsAppProvider provider,
            AppDbContext db,
            ILogger<WhatsAppService> logger,  
            HttpClient httpClient,
            IOptions<WhatsAppSettings> options,
            IMediaStorageService mediaStorage)
        {
            _provider = provider;
            _db = db;
            _logger = logger;
            _httpClient = httpClient;
            _settings = options.Value;
            _mediaStorage = mediaStorage;
        }

        public async Task<WhatsAppMessageResult> SendMessageAsync(SendWhatsAppMessageRequest request)
        {
            // Option 3: prefer ConversationId (unambiguous multi-number routing) but
            // still accept the legacy PhoneNumber path for backward compatibility.
            if (request.ConversationId is null && string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return new WhatsAppMessageResult
                {
                    Success = false,
                    Message = "Either ConversationId or PhoneNumber is required."
                };
            }

            var messageType = NormalizeMessageType(request.MessageType);

            if (messageType == "Text" && string.IsNullOrWhiteSpace(request.Message))
            {
                return new WhatsAppMessageResult
                {
                    Success = false,
                    Message = "Message is required for text messages."
                };
            }

            if (messageType != "Text" && string.IsNullOrWhiteSpace(request.MediaUrl))
            {
                return new WhatsAppMessageResult
                {
                    Success = false,
                    Message = $"MediaUrl is required for {messageType} messages."
                };
            }

            Customer? customer;
            Conversation conversation;

            if (request.ConversationId is Guid conversationId)
            {
                // Preferred path: the conversation already knows its business number.
                conversation = await _db.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId);

                if (conversation is null)
                {
                    return new WhatsAppMessageResult
                    {
                        Success = false,
                        Message = $"Conversation '{conversationId}' was not found."
                    };
                }

                customer = await _db.Customers
                    .FirstOrDefaultAsync(c => c.Id == conversation.CustomerId);

                if (customer is null)
                {
                    return new WhatsAppMessageResult
                    {
                        Success = false,
                        CustomerNotFound = true,
                        Message = "The conversation's customer could not be found."
                    };
                }
            }
            else
            {
                // Legacy path: resolve by the customer's WhatsApp number.
                var phoneLookup = NormalizePhone(request.PhoneNumber!);

                customer = await _db.Customers
                    .FirstOrDefaultAsync(c => c.WhatsAppNumber == phoneLookup);

                if (customer is null)
                {
                    return new WhatsAppMessageResult
                    {
                        Success = false,
                        CustomerNotFound = true,
                        Message = $"No customer found for WhatsApp number '{phoneLookup}'."
                    };
                }

                conversation = await GetOrCreateOpenConversationAsync(customer);
            }

            // The recipient number always comes from the customer, never the request,
            // so a ConversationId send is delivered to the correct person.
            var phone = NormalizePhone(customer.WhatsAppNumber);

            // Determine which WhatsApp Business number this conversation uses so the
            // Cloud API call goes out through the correct Meta PhoneNumberId. Falls
            // back to the global configured number for backward compatibility.
            var senderPhoneNumberId = await ResolveOutgoingPhoneNumberIdAsync(conversation);

            // Deliver the message for real through the Meta Cloud API so the
            // recipient actually receives it. The mock provider only simulates
            // delivery, so it is used purely as a fallback when Meta is not
            // configured (keeps the POC working offline).
            string? deliveredMessageId;

            if (messageType == "Text" && _settings.IsConfigured)
            {
                var metaResult = await SendToMetaAsync(
                    new
                    {
                        messaging_product = "whatsapp",
                        to = phone,
                        type = "text",
                        text = new { body = request.Message }
                    },
                    "text",
                    phone,
                    senderPhoneNumberId);

                if (!metaResult.Success)
                {
                    return new WhatsAppMessageResult
                    {
                        Success = false,
                        Message = metaResult.Error ?? "Failed to send the message to WhatsApp."
                    };
                }

                deliveredMessageId = metaResult.MessageId;
            }
            else if (messageType != "Text" && _settings.IsConfigured)
            {
                // Media (image/document): upload the file to Meta to obtain a media id,
                // then send a message that references it. Falls back to the mock
                // provider when the upload can't be performed.
                var metaResult = await SendMediaToMetaAsync(
                    phone, messageType, request.MediaUrl, request.Message, senderPhoneNumberId);

                if (!metaResult.Success)
                {
                    return new WhatsAppMessageResult
                    {
                        Success = false,
                        Message = metaResult.Error ?? "Failed to send the media to WhatsApp."
                    };
                }

                deliveredMessageId = metaResult.MessageId;
            }
            else
            {
                // Non-text messages (or unconfigured Meta) fall back to the mock provider.
                var providerResult = await _provider.SendMessageAsync(
                    phone, request.Message, messageType, request.MediaUrl);

                if (!providerResult.Success)
                {
                    return new WhatsAppMessageResult
                    {
                        Success = false,
                        Message = providerResult.Error ?? "Provider failed to send the message."
                    };
                }

                deliveredMessageId = providerResult.ProviderMessageId;
            }

            var chatMessage = new ChatMessages
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                Direction = "Outgoing",
                MessageType = messageType,
                MessageText = request.Message,
                MediaUrl = request.MediaUrl,
                WhatsAppMessageId = deliveredMessageId,
                CreatedAt = DateTime.UtcNow
            };

            _db.ChatMessages.Add(chatMessage);

            // The change tracker already detects this mutation on the tracked entity
            // (Added or Unchanged); calling Update() here would wrongly force a
            // brand-new conversation into Modified state and break SaveChanges.
            conversation.LastMessageAt = chatMessage.CreatedAt;

            await _db.SaveChangesAsync();

            return new WhatsAppMessageResult
            {
                Success = true,
                Message = "Message sent successfully.",
                Conversation = MapConversation(conversation, customer),
                ChatMessage = MapMessage(chatMessage)
            };
        }

        public async Task<WhatsAppMessageResult> ReceiveMessageAsync(ReceiveWhatsAppMessageRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return new WhatsAppMessageResult
                {
                    Success = false,
                    Message = "PhoneNumber is required."
                };
            }

            var messageType = NormalizeMessageType(request.MessageType);

            if (messageType == "Text" && string.IsNullOrWhiteSpace(request.Message))
            {
                return new WhatsAppMessageResult
                {
                    Success = false,
                    Message = "Message is required for text messages."
                };
            }

            var phone = NormalizePhone(request.PhoneNumber);

            // Resolve which registered WhatsApp Business number (and therefore which
            // sales person) this incoming message belongs to. Never guess: if Meta
            // sent a phone_number_id we don't recognise, refuse to assign it to a
            // random sales person.
            WhatsAppBusinessNumber? businessNumber = null;
            if (!string.IsNullOrWhiteSpace(request.MetaPhoneNumberId))
            {
                businessNumber = await _db.WhatsAppBusinessNumbers
                    .FirstOrDefaultAsync(n => n.PhoneNumberId == request.MetaPhoneNumberId && n.IsActive);

                if (businessNumber is null)
                {
                    _logger.LogWarning(
                        "Incoming WhatsApp message for unregistered phone_number_id {PhoneNumberId}. " +
                        "Message will not be assigned to any sales person.",
                        request.MetaPhoneNumberId);

                    return new WhatsAppMessageResult
                    {
                        Success = false,
                        Message = $"No registered WhatsApp Business number found for phone_number_id '{request.MetaPhoneNumberId}'."
                    };
                }
            }

            // Meta retries the webhook when it doesn't get a fast 200 OK (media
            // downloads make our handler slower, which triggers retries). Each retry
            // carries the same WhatsApp message id, so ignore messages we've already
            // stored to prevent duplicates in the chat box.
            if (!string.IsNullOrWhiteSpace(request.WhatsAppMessageId))
            {
                var existing = await _db.ChatMessages
                    .Include(m => m.Conversation)
                    .FirstOrDefaultAsync(m => m.WhatsAppMessageId == request.WhatsAppMessageId);

                if (existing is not null)
                {
                    _logger.LogInformation(
                        "Ignoring duplicate incoming WhatsApp message {MessageId}.",
                        request.WhatsAppMessageId);

                    var existingCustomer = existing.Conversation is not null
                        ? await _db.Customers.FirstOrDefaultAsync(c => c.Id == existing.Conversation.CustomerId)
                        : null;

                    return new WhatsAppMessageResult
                    {
                        Success = true,
                        Message = "Duplicate message ignored.",
                        Conversation = existing.Conversation is not null && existingCustomer is not null
                            ? MapConversation(existing.Conversation, existingCustomer)
                            : null,
                        ChatMessage = MapMessage(existing)
                    };
                }
            }

            var customer = await _db.Customers
                .FirstOrDefaultAsync(c => c.WhatsAppNumber == phone);

            if (customer is null)
            {
                customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    Name = string.IsNullOrWhiteSpace(request.CustomerName)
                        ? $"WhatsApp {phone}"
                        : request.CustomerName!,
                    WhatsAppNumber = phone,
                    // Assign the customer to the sales person who owns the business
                    // number that received this message.
                    AssignedUserId = businessNumber?.SalesUserId,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Customers.Add(customer);
                _logger.LogInformation("Created new customer for incoming WhatsApp number {Phone}.", phone);
            }
            else if (businessNumber is not null && customer.AssignedUserId is null)
            {
                // Existing customer with no owner yet: adopt the receiving number's owner.
                customer.AssignedUserId = businessNumber.SalesUserId;
            }

            var conversation = await GetOrCreateOpenConversationAsync(customer, businessNumber);

            // Incoming media arrives as a Meta media id. Download the bytes and store
            // them locally so the file can be opened/downloaded from the CRM.
            var mediaUrl = request.MediaUrl;
            if (string.IsNullOrWhiteSpace(mediaUrl) &&
                !string.IsNullOrWhiteSpace(request.MetaMediaId) &&
                _settings.IsConfigured)
            {
                var stored = await DownloadMetaMediaAsync(request.MetaMediaId!, request.FileName);
                if (stored is not null)
                {
                    mediaUrl = stored.Url;
                }
                else
                {
                    _logger.LogWarning(
                        "Could not download incoming media {MetaMediaId} for {Phone}.",
                        request.MetaMediaId, phone);
                }
            }

            var chatMessage = new ChatMessages
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                Direction = "Incoming",
                MessageType = messageType,
                MessageText = request.Message,
                MediaUrl = mediaUrl,
                WhatsAppMessageId = request.WhatsAppMessageId,
                CreatedAt = DateTime.UtcNow
            };

            _db.ChatMessages.Add(chatMessage);

            // The change tracker already detects this mutation on the tracked entity
            // (Added or Unchanged); calling Update() here would wrongly force a
            // brand-new conversation into Modified state and break SaveChanges.
            conversation.LastMessageAt = chatMessage.CreatedAt;

            await _db.SaveChangesAsync();

            return new WhatsAppMessageResult
            {
                Success = true,
                Message = "Incoming message received successfully.",
                Conversation = MapConversation(conversation, customer),
                ChatMessage = MapMessage(chatMessage)
            };
        }

        public async Task UpdateMessageStatusAsync(string whatsAppMessageId, string status)
        {
            if (string.IsNullOrWhiteSpace(whatsAppMessageId) || string.IsNullOrWhiteSpace(status))
            {
                return;
            }

            var message = await _db.ChatMessages
                .FirstOrDefaultAsync(m => m.WhatsAppMessageId == whatsAppMessageId);

            if (message is null)
            {
                _logger.LogInformation(
                    "Status webhook for unknown message id {MessageId} ignored.", whatsAppMessageId);
                return;
            }

            // Do not downgrade a more advanced status (e.g. read -> delivered) that can
            // arrive out of order from Meta.
            if (!ShouldApplyStatus(message.Status, status))
            {
                return;
            }

            message.Status = status;
            await _db.SaveChangesAsync();
        }

        private static bool ShouldApplyStatus(string? current, string next)
        {
            int Rank(string? s) => s?.ToLowerInvariant() switch
            {
                "failed" => 4,
                "read" => 3,
                "delivered" => 2,
                "sent" => 1,
                _ => 0
            };

            // "failed" always wins; otherwise only move forward.
            return string.Equals(next, "failed", StringComparison.OrdinalIgnoreCase)
                ? true
                : Rank(next) > Rank(current);
        }

        private async Task<Conversation> GetOrCreateOpenConversationAsync(
            Customer customer,
            WhatsAppBusinessNumber? businessNumber = null)
        {
            // Conversation identity is scoped by (customer + business number): the same
            // customer talking to two different WhatsApp business numbers must get two
            // separate conversations. When the business number is unknown (legacy/global
            // path), fall back to matching an open conversation for the customer.
            IQueryable<Conversation> query = _db.Conversations
                .Where(c => c.CustomerId == customer.Id && c.Status == "Open");

            query = businessNumber is not null
                ? query.Where(c => c.WhatsAppBusinessNumberId == businessNumber.Id ||
                                   c.WhatsAppBusinessNumberId == null)
                : query;

            var conversation = await query
                .OrderByDescending(c => c.WhatsAppBusinessNumberId != null)
                .ThenByDescending(c => c.LastMessageAt)
                .FirstOrDefaultAsync();

            if (conversation is null)
            {
                conversation = new Conversation
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    // Preserve the customer's assigned sales user, falling back to the
                    // business number's owner when the customer has none.
                    AssignedUserId = customer.AssignedUserId ?? businessNumber?.SalesUserId,
                    WhatsAppBusinessNumberId = businessNumber?.Id,
                    Status = "Open",
                    CreatedAt = DateTime.UtcNow,
                    LastMessageAt = DateTime.UtcNow
                };

                _db.Conversations.Add(conversation);
            }
            else if (businessNumber is not null && conversation.WhatsAppBusinessNumberId is null)
            {
                // Backfill the business number on a pre-existing conversation.
                conversation.WhatsAppBusinessNumberId = businessNumber.Id;
            }

            return conversation;
        }

        private static string NormalizePhone(string phoneNumber)
            => phoneNumber.Trim();

        /// <summary>
        /// Determines the Meta PhoneNumberId to send from, based on the conversation's
        /// associated WhatsApp Business number. Falls back to the globally configured
        /// PhoneNumberId to keep the single-number POC working.
        /// </summary>
        private async Task<string> ResolveOutgoingPhoneNumberIdAsync(Conversation conversation)
        {
            if (conversation.WhatsAppBusinessNumberId is Guid businessNumberId)
            {
                var businessNumber = await _db.WhatsAppBusinessNumbers
                    .FirstOrDefaultAsync(n => n.Id == businessNumberId);

                if (businessNumber is not null && !string.IsNullOrWhiteSpace(businessNumber.PhoneNumberId))
                {
                    return businessNumber.PhoneNumberId;
                }
            }

            return _settings.PhoneNumberId;
        }

        private static string NormalizeMessageType(string? messageType)
        {
            if (string.IsNullOrWhiteSpace(messageType))
            {
                return "Text";
            }

            return messageType.Trim().ToLowerInvariant() switch
            {
                "image" => "Image",
                "document" => "Document",
                _ => "Text"
            };
        }

        private static ConversationDto MapConversation(Conversation conversation, Customer customer)
            => new()
            {
                Id = conversation.Id,
                CustomerId = conversation.CustomerId,
                CustomerName = customer.Name,
                CustomerWhatsAppNumber = customer.WhatsAppNumber,
                AssignedUserId = conversation.AssignedUserId,
                WhatsAppBusinessNumberId = conversation.WhatsAppBusinessNumberId,
                WhatsAppDisplayPhoneNumber = conversation.WhatsAppBusinessNumber?.DisplayPhoneNumber,
                Status = conversation.Status,
                CreatedAt = conversation.CreatedAt,
                LastMessageAt = conversation.LastMessageAt
            };

        private static ChatMessageDto MapMessage(ChatMessages message)
            => new()
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                Direction = message.Direction,
                MessageType = message.MessageType,
                MessageText = message.MessageText,
                MediaUrl = message.MediaUrl,
                WhatsAppMessageId = message.WhatsAppMessageId,
                Status = message.Status,
                TemplateName = message.TemplateName,
                CreatedAt = message.CreatedAt
            };

        ///////////////////
        /// Meta WhatsApp Cloud API integration
        ///

        public async Task<GetTemplatesResult> GetTemplatesAsync(bool approvedOnly = true)
        {
            if (string.IsNullOrWhiteSpace(_settings.WabaId) ||
                string.IsNullOrWhiteSpace(_settings.AccessToken))
            {
                _logger.LogError(
                    "Cannot fetch WhatsApp templates: WABA id or access token is not configured.");

                return new GetTemplatesResult
                {
                    Success = false,
                    Error = "WhatsApp templates are not configured on the server."
                };
            }

            // Real Meta source of truth: GET /{WABA_ID}/message_templates
            var url =
                $"{_settings.BaseUrl}/{_settings.ApiVersion}/{_settings.WabaId}/message_templates" +
                "?fields=name,language,status,category,components&limit=200";

            _logger.LogInformation(
                "Fetching WhatsApp templates from Meta WABA: {WabaId}", _settings.WabaId);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.AccessToken);

            try
            {
                using var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Fetching WhatsApp templates failed. Status: {StatusCode}. Meta response: {Response}",
                        (int)response.StatusCode, responseContent);

                    return new GetTemplatesResult
                    {
                        Success = false,
                        Error = ExtractMetaError(responseContent, response.StatusCode)
                    };
                }

                var templates = ParseTemplates(responseContent, approvedOnly);

                _logger.LogInformation(
                    "Meta returned {Count} WhatsApp template(s){Filter}.",
                    templates.Count, approvedOnly ? " (APPROVED only)" : string.Empty);

                return new GetTemplatesResult
                {
                    Success = true,
                    Templates = templates
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "Network error while fetching WhatsApp templates from the Meta Graph API.");

                return new GetTemplatesResult
                {
                    Success = false,
                    Error = "Failed to reach the WhatsApp service. Please try again later."
                };
            }
        }

        /// <summary>
        /// Maps Meta's /message_templates payload to CRM template DTOs. Meta returns the
        /// language under "language"; the CRM exposes it as "languageCode". When
        /// <paramref name="approvedOnly"/> is true, only APPROVED templates are returned.
        /// </summary>
        private static List<WhatsAppTemplateDto> ParseTemplates(string responseContent, bool approvedOnly)
        {
            var results = new List<WhatsAppTemplateDto>();

            using var doc = JsonDocument.Parse(responseContent);

            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                return results;
            }

            foreach (var item in data.EnumerateArray())
            {
                var status = item.TryGetProperty("status", out var statusEl)
                    ? statusEl.GetString() ?? string.Empty
                    : string.Empty;

                if (approvedOnly &&
                    !string.Equals(status, "APPROVED", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                results.Add(new WhatsAppTemplateDto
                {
                    Name = item.TryGetProperty("name", out var nameEl)
                        ? nameEl.GetString() ?? string.Empty
                        : string.Empty,
                    LanguageCode = item.TryGetProperty("language", out var langEl)
                        ? langEl.GetString() ?? string.Empty
                        : string.Empty,
                    Status = status,
                    Category = item.TryGetProperty("category", out var catEl)
                        ? catEl.GetString() ?? string.Empty
                        : string.Empty,
                    Components = item.TryGetProperty("components", out var compEl)
                        ? compEl.Clone()
                        : null
                });
            }

            return results;
        }

        public async Task<WhatsAppSendMessageResponse> SendTemplateMessageAsync(
            string recipientPhoneNumber,
            string templateName,
            string languageCode,
            List<string>? parameters = null,
            string? senderPhoneNumberId = null,
            string? senderAccessToken = null)
        {
            var template = new Dictionary<string, object>
            {
                ["name"] = templateName,
                ["language"] = new
                {
                    code = languageCode
                }
            };

            if (parameters != null && parameters.Count > 0)
            {
                template["components"] = new[]
                {
                    new
                    {
                        type = "body",
                        parameters = parameters.Select(parameter => new
                        {
                            type = "text",
                            text = parameter
                        }).ToArray()
                    }
                };
            }

            var requestBody = new
            {
                messaging_product = "whatsapp",
                to = recipientPhoneNumber,
                type = "template",
                template
            };

            return await SendToMetaAsync(requestBody, "template", recipientPhoneNumber, senderPhoneNumberId, senderAccessToken);
        }

        public async Task<SendTemplateMessageResponse> SendTemplateToCustomerAsync(
            SendTemplateToCustomerRequest request)
        {
            // 1. The selected WhatsApp Business number is the source of truth for the
            //    sender. It is required; outbound sending never falls back to the global
            //    (test) PhoneNumberId. The caller may identify it directly by
            //    WhatsAppBusinessNumberId, or indirectly by SalesUserId (the backend then
            //    resolves that salesperson's active business number).
            WhatsAppBusinessNumber? businessNumber = null;

            if (request.WhatsAppBusinessNumberId is Guid selectedBusinessNumberId &&
                selectedBusinessNumberId != Guid.Empty)
            {
                businessNumber = await _db.WhatsAppBusinessNumbers
                    .FirstOrDefaultAsync(n => n.Id == selectedBusinessNumberId);

                if (businessNumber is null)
                {
                    return new SendTemplateMessageResponse
                    {
                        Success = false,
                        Error = "The selected WhatsApp Business number was not found.",
                        Details = $"No WhatsApp Business number exists with id '{selectedBusinessNumberId}'."
                    };
                }
            }
            else if (request.SalesUserId is Guid selectedSalesUserId &&
                     selectedSalesUserId != Guid.Empty)
            {
                var salesUserNumbers = await _db.WhatsAppBusinessNumbers
                    .Where(n => n.SalesUserId == selectedSalesUserId && n.IsActive)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                if (salesUserNumbers.Count == 0)
                {
                    return new SendTemplateMessageResponse
                    {
                        Success = false,
                        Error = "The selected WhatsApp Business number was not found.",
                        Details = $"The selected salesperson '{selectedSalesUserId}' has no active WhatsApp Business number."
                    };
                }

                if (salesUserNumbers.Count > 1)
                {
                    return new SendTemplateMessageResponse
                    {
                        Success = false,
                        Error = "Multiple WhatsApp Business numbers found for the selected salesperson.",
                        Details = "Specify WhatsAppBusinessNumberId because the salesperson owns more than one active business number."
                    };
                }

                businessNumber = salesUserNumbers[0];
            }
            else
            {
                return new SendTemplateMessageResponse
                {
                    Success = false,
                    Error = "WhatsAppBusinessNumberId is required.",
                    Details = "A WhatsApp Business number (or SalesUserId) must be selected to send an outbound message."
                };
            }

            if (!businessNumber.IsActive)
            {
                return new SendTemplateMessageResponse
                {
                    Success = false,
                    Error = "WhatsApp business number is inactive.",
                    Details = $"The selected WhatsApp business number '{businessNumber.DisplayPhoneNumber}' is not active."
                };
            }

            if (string.IsNullOrWhiteSpace(businessNumber.PhoneNumberId))
            {
                return new SendTemplateMessageResponse
                {
                    Success = false,
                    Error = "The selected WhatsApp Business number is not configured with a Meta PhoneNumberId.",
                    Details = "The selected WhatsApp business number has no Meta phone_number_id."
                };
            }

            // 2. Resolve the sender access token: per-number token when present,
            //    otherwise the globally configured token. Never logged/exposed.
            var senderAccessToken = string.IsNullOrWhiteSpace(businessNumber.AccessToken)
                ? _settings.AccessToken
                : businessNumber.AccessToken;

            if (string.IsNullOrWhiteSpace(senderAccessToken))
            {
                return new SendTemplateMessageResponse
                {
                    Success = false,
                    Error = "WhatsApp integration is not configured for the selected business number.",
                    Details = "No access token is available for the selected WhatsApp business number."
                };
            }

            var senderPhoneNumberId = businessNumber.PhoneNumberId;

            // 3. Resolve the customer. Case A: CustomerId supplied -> use it.
            //    Case B: no CustomerId but RecipientPhoneNumber supplied -> find or
            //    create the CRM customer by phone number.
            Customer? customer = null;
            if (request.CustomerId is Guid customerId && customerId != Guid.Empty)
            {
                customer = await _db.Customers
                    .FirstOrDefaultAsync(c => c.Id == customerId);

                if (customer is null)
                {
                    return new SendTemplateMessageResponse
                    {
                        Success = false,
                        Error = "Customer not found.",
                        Details = $"No customer exists with id '{customerId}'."
                    };
                }
            }
            else if (!string.IsNullOrWhiteSpace(request.RecipientPhoneNumber))
            {
                var lookupPhone = NormalizePhone(request.RecipientPhoneNumber);

                customer = await _db.Customers
                    .FirstOrDefaultAsync(c => c.WhatsAppNumber == lookupPhone);

                if (customer is null)
                {
                    customer = new Customer
                    {
                        Id = Guid.NewGuid(),
                        Name = string.IsNullOrWhiteSpace(request.CustomerName)
                            ? $"WhatsApp {lookupPhone}"
                            : request.CustomerName!,
                        WhatsAppNumber = lookupPhone,
                        // Associate the new customer with the salesperson who owns the
                        // selected business number.
                        AssignedUserId = businessNumber.SalesUserId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _db.Customers.Add(customer);
                    _logger.LogInformation(
                        "Created new customer for outbound WhatsApp number {Phone}.",
                        MaskPhone(lookupPhone));
                }
                else if (customer.AssignedUserId is null)
                {
                    customer.AssignedUserId = businessNumber.SalesUserId;
                }
            }
            else
            {
                return new SendTemplateMessageResponse
                {
                    Success = false,
                    Error = "A customer or recipient phone number is required.",
                    Details = "Supply either CustomerId or RecipientPhoneNumber."
                };
            }

            // 4. Validate the customer has a WhatsApp number.
            if (string.IsNullOrWhiteSpace(customer.WhatsAppNumber))
            {
                return new SendTemplateMessageResponse
                {
                    Success = false,
                    Error = "Customer has no WhatsApp number.",
                    Details = "The selected customer does not have a WhatsApp number on record."
                };
            }

            var phone = NormalizePhone(customer.WhatsAppNumber);

            // 5. Resolve the conversation. Prefer the caller-supplied conversation when
            //    valid and belonging to this customer; otherwise reuse/create the
            //    customer's open conversation scoped to the selected business number.
            Conversation? conversation = null;
            if (request.ConversationId is Guid conversationId)
            {
                conversation = await _db.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId && c.CustomerId == customer.Id);
            }

            conversation ??= await GetOrCreateOpenConversationAsync(customer, businessNumber);

            var textParameters = request.Parameters
                .Where(p => string.Equals(p.Type, "text", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Text)
                .ToList();

            // 5b. Validate the selected template + language actually exists and is
            // APPROVED in the configured Meta WABA. Never call Meta with an arbitrary
            // (potentially fake) template name.
            var templatesResult = await GetTemplatesAsync(approvedOnly: true);
            if (templatesResult.Success)
            {
                var match = templatesResult.Templates.FirstOrDefault(t =>
                    string.Equals(t.Name, request.TemplateName, StringComparison.Ordinal) &&
                    string.Equals(t.LanguageCode, request.LanguageCode, StringComparison.OrdinalIgnoreCase));

                if (match is null)
                {
                    _logger.LogWarning(
                        "Rejected send: template '{Template}' ({Language}) is not an APPROVED template in WABA {WabaId}.",
                        request.TemplateName, request.LanguageCode, _settings.WabaId);

                    return new SendTemplateMessageResponse
                    {
                        Success = false,
                        Error = "Selected WhatsApp template is not an approved template in the configured Meta WhatsApp Business Account.",
                        Details = $"Template '{request.TemplateName}' with language '{request.LanguageCode}' was not found among APPROVED templates."
                    };
                }

                _logger.LogInformation(
                    "Selected template: {TemplateName}, language: {LanguageCode}, status: {Status}",
                    match.Name, match.LanguageCode, match.Status);
            }
            else
            {
                // If we cannot verify against Meta, do not risk sending a fake template.
                return new SendTemplateMessageResponse
                {
                    Success = false,
                    Error = "Could not verify the template against the Meta WhatsApp Business Account.",
                    Details = templatesResult.Error
                };
            }

            // 6. Send through the existing template method (builds template +
            // components/parameters and calls the Meta Cloud API).
            var metaResult = await SendTemplateMessageAsync(
                phone,
                request.TemplateName,
                request.LanguageCode,
                textParameters.Count > 0 ? textParameters : null,
                senderPhoneNumberId,
                senderAccessToken);

            // 7. Do NOT persist the message as sent when Meta returns an error.
            if (!metaResult.Success)
            {
                _logger.LogWarning(
                    "Template '{Template}' send failed for customer {CustomerId}: {Error}",
                    request.TemplateName, customer.Id, metaResult.Error);

                return new SendTemplateMessageResponse
                {
                    Success = false,
                    ConversationId = conversation.Id,
                    Error = "Meta WhatsApp API error",
                    Details = metaResult.Error
                };
            }

            // 8. Persist the outgoing message against the conversation.
            // Ensure the conversation is tied to the sender business number.
            if (conversation.WhatsAppBusinessNumberId != businessNumber.Id)
            {
                conversation.WhatsAppBusinessNumberId = businessNumber.Id;
            }

            var chatMessage = new ChatMessages
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                Direction = "Outgoing",
                MessageType = "Text",
                MessageText = BuildTemplatePreview(request.TemplateName, textParameters),
                WhatsAppMessageId = metaResult.MessageId,
                Status = "sent",
                TemplateName = request.TemplateName,
                CreatedAt = DateTime.UtcNow
            };

            _db.ChatMessages.Add(chatMessage);
            conversation.LastMessageAt = chatMessage.CreatedAt;

            await _db.SaveChangesAsync();

            return new SendTemplateMessageResponse
            {
                Success = true,
                MessageId = metaResult.MessageId,
                ConversationId = conversation.Id
            };
        }

        private static string BuildTemplatePreview(string templateName, List<string> parameters)
            => parameters.Count == 0
                ? $"[Template: {templateName}]"
                : $"[Template: {templateName}] {string.Join(" | ", parameters)}";

        public async Task<WhatsAppSendMessageResponse> SendTextMessageAsync(
            string recipientPhoneNumber,
            string message)
        {
            var requestBody = new
            {
                messaging_product = "whatsapp",
                to = recipientPhoneNumber,
                type = "text",
                text = new
                {
                    body = message
                }
            };

            var response = await SendToMetaAsync(requestBody, "text", recipientPhoneNumber);

            // Persist the outgoing message so it appears in the CRM inbox/conversation
            // history, mirroring the behaviour of the mock send/receive flow.
            if (response.Success)
            {
                await PersistOutgoingMessageAsync(
                    recipientPhoneNumber,
                    message,
                    "Text",
                    null,
                    response.MessageId);
            }

            return response;
        }

        /// <summary>
        /// Stores an outgoing message against the recipient's open conversation,
        /// creating the customer and/or conversation when they do not yet exist.
        /// This keeps Meta-sent messages visible in the CRM inbox.
        /// </summary>
        private async Task PersistOutgoingMessageAsync(
            string recipientPhoneNumber,
            string? messageText,
            string messageType,
            string? mediaUrl,
            string? whatsAppMessageId)
        {
            var phone = NormalizePhone(recipientPhoneNumber);

            var customer = await _db.Customers
                .FirstOrDefaultAsync(c => c.WhatsAppNumber == phone);

            if (customer is null)
            {
                customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    Name = $"WhatsApp {phone}",
                    WhatsAppNumber = phone,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Customers.Add(customer);
                _logger.LogInformation(
                    "Created new customer for outgoing WhatsApp number {Phone}.", phone);
            }

            var conversation = await GetOrCreateOpenConversationAsync(customer);

            var chatMessage = new ChatMessages
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                Direction = "Outgoing",
                MessageType = messageType,
                MessageText = messageText,
                MediaUrl = mediaUrl,
                WhatsAppMessageId = whatsAppMessageId,
                CreatedAt = DateTime.UtcNow
            };

            _db.ChatMessages.Add(chatMessage);
            conversation.LastMessageAt = chatMessage.CreatedAt;

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Sends a payload to the Meta Graph messages endpoint and returns a clean,
        /// token-free result. Never logs or returns the access token.
        /// </summary>
        private async Task<WhatsAppSendMessageResponse> SendToMetaAsync(
            object requestBody,
            string messageKind,
            string recipientPhoneNumber,
            string? senderPhoneNumberId = null,
            string? senderAccessToken = null)
        {
            var phoneNumberId = string.IsNullOrWhiteSpace(senderPhoneNumberId)
                ? _settings.PhoneNumberId
                : senderPhoneNumberId;

            var accessToken = string.IsNullOrWhiteSpace(senderAccessToken)
                ? _settings.AccessToken
                : senderAccessToken;

            if (string.IsNullOrWhiteSpace(phoneNumberId) ||
                string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError(
                    "WhatsApp configuration is incomplete. PhoneNumberId/AccessToken are not set.");

                return new WhatsAppSendMessageResponse
                {
                    Success = false,
                    Error = "WhatsApp integration is not configured on the server."
                };
            }

            var url =
                $"{_settings.BaseUrl}/{_settings.ApiVersion}/{phoneNumberId}/messages";

            var json = JsonSerializer.Serialize(requestBody);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                using var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Log Meta's error body (no secrets are present in the response body).
                    _logger.LogError(
                        "WhatsApp {MessageKind} send failed. Status: {StatusCode}. Meta response: {Response}",
                        messageKind, (int)response.StatusCode, responseContent);

                    return new WhatsAppSendMessageResponse
                    {
                        Success = false,
                        Error = ExtractMetaError(responseContent, response.StatusCode)
                    };
                }

                var messageId = ExtractMessageId(responseContent);

                _logger.LogInformation(
                    "WhatsApp {MessageKind} message sent to {Recipient}. MessageId: {MessageId}",
                    messageKind, MaskPhone(recipientPhoneNumber), messageId);

                return new WhatsAppSendMessageResponse
                {
                    Success = true,
                    MessageId = messageId
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "Network error while calling the WhatsApp Cloud API for a {MessageKind} message.",
                    messageKind);

                return new WhatsAppSendMessageResponse
                {
                    Success = false,
                    Error = "Failed to reach the WhatsApp service. Please try again later."
                };
            }
        }

        /// <summary>
        /// Delivers an image/document through the Meta Cloud API using the two-step
        /// media flow: (1) upload the stored file to /media to obtain a media id,
        /// then (2) send a message that references that id. The file bytes come from
        /// the local media storage (the same place the upload endpoint saved them).
        /// </summary>
        private async Task<WhatsAppSendMessageResponse> SendMediaToMetaAsync(
            string recipientPhoneNumber,
            string messageType,
            string? mediaUrl,
            string? caption,
            string? senderPhoneNumberId = null)
        {
            if (string.IsNullOrWhiteSpace(mediaUrl))
            {
                return new WhatsAppSendMessageResponse
                {
                    Success = false,
                    Error = "MediaUrl is required to send media."
                };
            }

            var phoneNumberId = string.IsNullOrWhiteSpace(senderPhoneNumberId)
                ? _settings.PhoneNumberId
                : senderPhoneNumberId;

            if (string.IsNullOrWhiteSpace(phoneNumberId) ||
                string.IsNullOrWhiteSpace(_settings.AccessToken))
            {
                return new WhatsAppSendMessageResponse
                {
                    Success = false,
                    Error = "WhatsApp integration is not configured on the server."
                };
            }

            // MediaUrl is stored as "/uploads/{storedFileName}". Open the local file.
            var storedFileName = Path.GetFileName(mediaUrl);
            var media = _mediaStorage.Open(storedFileName);

            if (media is null)
            {
                return new WhatsAppSendMessageResponse
                {
                    Success = false,
                    Error = "The media file could not be found on the server."
                };
            }

            string? mediaId;
            await using (media.Content)
            {
                mediaId = await UploadMediaToMetaAsync(media, phoneNumberId);
            }

            if (string.IsNullOrWhiteSpace(mediaId))
            {
                return new WhatsAppSendMessageResponse
                {
                    Success = false,
                    Error = "Failed to upload the media to WhatsApp."
                };
            }

            var isImage = string.Equals(messageType, "Image", StringComparison.OrdinalIgnoreCase);
            var metaType = isImage ? "image" : "document";

            object mediaPayload = isImage
                ? new { id = mediaId, caption = string.IsNullOrWhiteSpace(caption) ? null : caption }
                : new
                {
                    id = mediaId,
                    caption = string.IsNullOrWhiteSpace(caption) ? null : caption,
                    filename = media.OriginalFileName
                };

            var requestBody = new Dictionary<string, object>
            {
                ["messaging_product"] = "whatsapp",
                ["to"] = recipientPhoneNumber,
                ["type"] = metaType,
                [metaType] = mediaPayload
            };

            return await SendToMetaAsync(requestBody, metaType, recipientPhoneNumber, phoneNumberId);
        }

        /// <summary>
        /// Uploads a file to the Meta /media endpoint and returns the media id.
        /// </summary>
        private async Task<string?> UploadMediaToMetaAsync(StoredMediaFile media, string? senderPhoneNumberId = null)
        {
            var phoneNumberId = string.IsNullOrWhiteSpace(senderPhoneNumberId)
                ? _settings.PhoneNumberId
                : senderPhoneNumberId;

            var url = $"{_settings.BaseUrl}/{_settings.ApiVersion}/{phoneNumberId}/media";

            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(media.Content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(media.ContentType);

            content.Add(fileContent, "file", media.OriginalFileName);
            content.Add(new StringContent("whatsapp"), "messaging_product");
            content.Add(new StringContent(media.ContentType), "type");

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.AccessToken);
            request.Content = content;

            try
            {
                using var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "WhatsApp media upload failed. Status: {StatusCode}. Meta response: {Response}",
                        (int)response.StatusCode, responseContent);
                    return null;
                }

                using var doc = JsonDocument.Parse(responseContent);
                return doc.RootElement.TryGetProperty("id", out var idEl)
                    ? idEl.GetString()
                    : null;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error while uploading media to the WhatsApp Cloud API.");
                return null;
            }
        }

        /// <summary>
        /// Downloads an incoming media file from Meta (by media id) and stores it
        /// locally, returning the stored media so it can be opened/downloaded from
        /// the CRM. Returns null when the download can't be performed.
        /// </summary>
        private async Task<StoredMedia?> DownloadMetaMediaAsync(string metaMediaId, string? preferredFileName)
        {
            if (string.IsNullOrWhiteSpace(_settings.PhoneNumberId) ||
                string.IsNullOrWhiteSpace(_settings.AccessToken))
            {
                return null;
            }

            try
            {
                // Step 1: resolve the media id to a temporary download URL.
                var metadataUrl = $"{_settings.BaseUrl}/{_settings.ApiVersion}/{metaMediaId}";

                using var metaRequest = new HttpRequestMessage(HttpMethod.Get, metadataUrl);
                metaRequest.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _settings.AccessToken);

                using var metaResponse = await _httpClient.SendAsync(metaRequest);
                var metaContent = await metaResponse.Content.ReadAsStringAsync();

                if (!metaResponse.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Failed to resolve Meta media metadata. Status: {StatusCode}. Response: {Response}",
                        (int)metaResponse.StatusCode, metaContent);
                    return null;
                }

                string? downloadUrl;
                string? mimeType;
                using (var doc = JsonDocument.Parse(metaContent))
                {
                    downloadUrl = doc.RootElement.TryGetProperty("url", out var urlEl)
                        ? urlEl.GetString()
                        : null;
                    mimeType = doc.RootElement.TryGetProperty("mime_type", out var mimeEl)
                        ? mimeEl.GetString()
                        : null;
                }

                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    return null;
                }

                // Step 2: download the actual bytes (also requires the bearer token).
                using var fileRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                fileRequest.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _settings.AccessToken);

                using var fileResponse = await _httpClient.SendAsync(fileRequest);

                if (!fileResponse.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Failed to download Meta media bytes. Status: {StatusCode}.",
                        (int)fileResponse.StatusCode);
                    return null;
                }

                var contentType = fileResponse.Content.Headers.ContentType?.ToString()
                    ?? mimeType
                    ?? "application/octet-stream";

                var fileName = string.IsNullOrWhiteSpace(preferredFileName)
                    ? BuildFileNameFromContentType(metaMediaId, contentType)
                    : preferredFileName!;

                await using var contentStream = await fileResponse.Content.ReadAsStreamAsync();
                return await _mediaStorage.SaveAsync(contentStream, fileName, contentType);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error while downloading media from the WhatsApp Cloud API.");
                return null;
            }
        }

        private static string BuildFileNameFromContentType(string mediaId, string contentType)
        {
            var extension = contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                "application/pdf" => ".pdf",
                _ => string.Empty
            };

            return $"{mediaId}{extension}";
        }

        private static string? ExtractMessageId(string responseContent)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseContent);
                if (doc.RootElement.TryGetProperty("messages", out var messages) &&
                    messages.ValueKind == JsonValueKind.Array &&
                    messages.GetArrayLength() > 0 &&
                    messages[0].TryGetProperty("id", out var id))
                {
                    return id.GetString();
                }
            }
            catch (JsonException)
            {
                // Ignore parse issues; caller still gets a success flag.
            }

            return null;
        }

        private static string ExtractMetaError(
            string responseContent,
            System.Net.HttpStatusCode statusCode)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseContent);
                if (doc.RootElement.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("message", out var message))
                {
                    return message.GetString() ?? $"WhatsApp API error ({(int)statusCode}).";
                }
            }
            catch (JsonException)
            {
                // Fall through to a generic message.
            }

            return $"WhatsApp API error ({(int)statusCode}).";
        }

        private static string MaskPhone(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length <= 4)
            {
                return "****";
            }

            return string.Concat(new string('*', phoneNumber.Length - 4), phoneNumber.AsSpan(phoneNumber.Length - 4));
        }
    }
}
