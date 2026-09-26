using Microsoft.AspNetCore.Mvc;
using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Models;
using WhatsAppDemo.Api.Services;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace WhatsAppDemo.Api.Controllers
{
    [ApiController]
    [Route("api/whatsapp")]
    public class WhatsAppController : ControllerBase
    {
        private readonly IWhatsAppService _whatsAppService;
        private readonly WhatsAppSettings _settings;
        private readonly ILogger<WhatsAppController> _logger;
        private readonly IMediaStorageService _mediaStorage;

        public WhatsAppController(
            IWhatsAppService whatsAppService,
            IOptions<WhatsAppSettings> options,
            ILogger<WhatsAppController> logger,
            IMediaStorageService mediaStorage)
        {
            _whatsAppService = whatsAppService;
            _settings = options.Value;
            _logger = logger;
            _mediaStorage = mediaStorage;
        }

        /// <summary>
        /// Meta webhook verification (GET challenge). Meta calls this once when you
        /// subscribe the webhook URL in the Meta App dashboard.
        /// </summary>
        [HttpGet("webhook")]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? verifyToken,
            [FromQuery(Name = "hub.challenge")] string? challenge)
        {
            if (mode == "subscribe" && verifyToken == _settings.VerifyToken)
            {
                return Content(challenge ?? string.Empty, "text/plain");
            }

            return Forbid();
        }

        /// <summary>
        /// Sends an outgoing WhatsApp message (mock) and stores it against the customer's conversation.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/whatsapp/send
        ///     {
        ///        "phoneNumber": "919999999999",
        ///        "message": "Hello",
        ///        "messageType": "Text",
        ///        "mediaUrl": null
        ///     }
        /// </remarks>
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendWhatsAppMessageRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _whatsAppService.SendMessageAsync(request);

            if (!result.Success)
            {
                if (result.CustomerNotFound)
                {
                    return NotFound(result);
                }

                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Uploads an image or document that a sales person wants to share in a
        /// conversation. Returns the stored media URL and inferred message type,
        /// which the caller then passes to <c>POST /api/whatsapp/send</c>.
        /// </summary>
        /// <remarks>
        /// Send the file as multipart/form-data using the form field name "file".
        /// </remarks>
        [HttpPost("upload")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadMedia(IFormFile? file)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "A non-empty file is required." });
            }

            await using var stream = file.OpenReadStream();
            var stored = await _mediaStorage.SaveAsync(stream, file.FileName, file.ContentType);

            return Ok(new
            {
                mediaUrl = stored.Url,
                messageType = stored.MessageType,
                fileName = stored.OriginalFileName,
                contentType = stored.ContentType
            });
        }

        /// <summary>
        /// Downloads a previously stored media file with its original file name,
        /// so documents/images shared in a conversation can be saved by the user.
        /// </summary>
        [HttpGet("media/{fileName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult DownloadMedia(string fileName)
        {
            var media = _mediaStorage.Open(fileName);

            if (media is null)
            {
                return NotFound(new { message = $"Media '{fileName}' was not found." });
            }

            return File(media.Content, media.ContentType, media.OriginalFileName);
        }

        /// <summary>
        /// Receives incoming WhatsApp messages. Handles both Meta's real webhook
        /// payload (nested entry/changes/value/messages structure) and the simple
        /// mock payload used for local testing.
        /// </summary>
        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveWebhook([FromBody] JsonElement body)
        {
            // Diagnostic: log the full raw payload so we can confirm Meta is reaching
            // this endpoint and inspect exactly what it sends.
            _logger.LogInformation(
                "Incoming WhatsApp webhook POST received. Raw payload: {Payload}",
                body.GetRawText());

            // Try Meta's real payload first.
            var metaRequests = TryParseMetaWebhook(body);

            if (metaRequests.Count > 0)
            {
                foreach (var req in metaRequests)
                {
                    var metaResult = await _whatsAppService.ReceiveMessageAsync(req);
                    if (!metaResult.Success)
                    {
                        _logger.LogWarning(
                            "Failed to persist incoming WhatsApp message: {Message}", metaResult.Message);
                    }
                }

                // Meta expects a 200 OK acknowledgement.
                return Ok(new { received = metaRequests.Count });
            }

            // Fall back to the simple mock payload.
            var mock = ParseMockWebhook(body);

            if (mock is null || string.IsNullOrWhiteSpace(mock.PhoneNumber))
            {
                return BadRequest(new { message = "Unrecognized webhook payload." });
            }

            var result = await _whatsAppService.ReceiveMessageAsync(mock);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Extracts incoming text messages from Meta's webhook payload.
        /// </summary>
        private static List<ReceiveWhatsAppMessageRequest> TryParseMetaWebhook(JsonElement body)
        {
            var results = new List<ReceiveWhatsAppMessageRequest>();

            if (body.ValueKind != JsonValueKind.Object ||
                !body.TryGetProperty("entry", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                return results;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes) ||
                    changes.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value))
                    {
                        continue;
                    }

                    // Map wa_id -> contact name for nicer customer naming.
                    var contactNames = new Dictionary<string, string>();
                    if (value.TryGetProperty("contacts", out var contacts) &&
                        contacts.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var contact in contacts.EnumerateArray())
                        {
                            if (contact.TryGetProperty("wa_id", out var waId) &&
                                contact.TryGetProperty("profile", out var profile) &&
                                profile.TryGetProperty("name", out var name))
                            {
                                contactNames[waId.GetString() ?? string.Empty] = name.GetString() ?? string.Empty;
                            }
                        }
                    }

                    if (!value.TryGetProperty("messages", out var messages) ||
                        messages.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var message in messages.EnumerateArray())
                    {
                        var from = message.TryGetProperty("from", out var fromEl)
                            ? fromEl.GetString()
                            : null;

                        if (string.IsNullOrWhiteSpace(from))
                        {
                            continue;
                        }

                        var whatsAppMessageId = message.TryGetProperty("id", out var idEl)
                            ? idEl.GetString()
                            : null;

                        var type = message.TryGetProperty("type", out var typeEl)
                            ? typeEl.GetString()
                            : "text";

                        string? text = null;
                        string? mediaUrl = null;
                        string? metaMediaId = null;
                        string? fileName = null;
                        var messageType = "Text";

                        switch (type)
                        {
                            case "text":
                                if (message.TryGetProperty("text", out var textEl) &&
                                    textEl.TryGetProperty("body", out var bodyEl))
                                {
                                    text = bodyEl.GetString();
                                }
                                break;

                            case "image":
                            case "document":
                            case "audio":
                            case "video":
                                messageType = type == "image"
                                    ? "Image"
                                    : type == "document"
                                        ? "Document"
                                        : "Text";

                                if (message.TryGetProperty(type!, out var mediaEl))
                                {
                                    // Media messages carry the file reference via "id"
                                    // (fetched from Meta's Media API) or, rarely, a
                                    // direct "link". Captions live under "caption".
                                    if (mediaEl.TryGetProperty("link", out var linkEl))
                                    {
                                        mediaUrl = linkEl.GetString();
                                    }
                                    else if (mediaEl.TryGetProperty("id", out var mediaIdEl))
                                    {
                                        metaMediaId = mediaIdEl.GetString();
                                    }

                                    if (mediaEl.TryGetProperty("filename", out var fileNameEl))
                                    {
                                        fileName = fileNameEl.GetString();
                                    }

                                    if (mediaEl.TryGetProperty("caption", out var captionEl))
                                    {
                                        text = captionEl.GetString();
                                    }
                                }
                                break;

                            default:
                                // Unsupported/interactive types: keep a readable placeholder
                                // so the message is still stored and visible in the chat box.
                                text = $"[Unsupported message type: {type}]";
                                break;
                        }

                        contactNames.TryGetValue(from, out var customerName);

                        results.Add(new ReceiveWhatsAppMessageRequest
                        {
                            PhoneNumber = from!,
                            Message = text,
                            MessageType = messageType,
                            MediaUrl = mediaUrl,
                            MetaMediaId = metaMediaId,
                            FileName = fileName,
                            WhatsAppMessageId = whatsAppMessageId,
                            CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Parses the simple mock webhook payload.
        /// </summary>
        private static ReceiveWhatsAppMessageRequest? ParseMockWebhook(JsonElement body)
        {
            if (body.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? GetString(string name) =>
                body.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
                    ? el.GetString()
                    : null;

            var phone = GetString("phoneNumber");
            if (string.IsNullOrWhiteSpace(phone))
            {
                return null;
            }

            return new ReceiveWhatsAppMessageRequest
            {
                PhoneNumber = phone,
                Message = GetString("message"),
                MessageType = GetString("messageType") ?? "Text",
                MediaUrl = GetString("mediaUrl"),
                WhatsAppMessageId = GetString("whatsappMessageId"),
                CustomerName = GetString("customerName")
            };
        }

        /// <summary>
        /// Sends an approved WhatsApp template message through the Meta Cloud API.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/whatsapp/send-template
        ///     {
        ///        "recipientPhoneNumber": "917XXXXXXXXX",
        ///        "templateName": "jaspers_market_order_confirmation_v1",
        ///        "languageCode": "en_US"
        ///     }
        /// </remarks>
        [HttpPost("send-template")]
        [ProducesResponseType(typeof(WhatsAppSendMessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WhatsAppSendMessageResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SendTemplate([FromBody] SendTemplateMessageRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _whatsAppService.SendTemplateMessageAsync(
                request.RecipientPhoneNumber,
                request.TemplateName,
                request.LanguageCode);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Sends a free-form WhatsApp text message through the Meta Cloud API.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/whatsapp/send-text
        ///     {
        ///        "recipientPhoneNumber": "917XXXXXXXXX",
        ///        "message": "Hello from my CRM"
        ///     }
        /// </remarks>
        [HttpPost("send-text")]
        [ProducesResponseType(typeof(WhatsAppSendMessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WhatsAppSendMessageResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SendText([FromBody] SendTextMessageRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _whatsAppService.SendTextMessageAsync(
                request.RecipientPhoneNumber,
                request.Message);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}
