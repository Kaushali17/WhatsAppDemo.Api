using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppDemo.Api.Data;
using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Models;

namespace WhatsAppDemo.Api.Controllers
{
    [ApiController]
    [Route("api/conversations")]
    public class ConversationsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ConversationsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ConversationDto>>> GetAll()
        {
            var conversations = await _db.Conversations
                .Include(c => c.Customer)
                .Include(c => c.AssignedUser)
                .OrderByDescending(c => c.LastMessageAt)
                .ToListAsync();

            var conversationIds = conversations.Select(c => c.Id).ToList();

            // Fetch the most recent message per conversation for the list preview.
            var previews = await _db.ChatMessages
                .Where(m => conversationIds.Contains(m.ConversationId))
                .GroupBy(m => m.ConversationId)
                .Select(g => g.OrderByDescending(m => m.CreatedAt).First())
                .ToListAsync();

            var previewByConversation = previews.ToDictionary(
                m => m.ConversationId,
                m => m.MessageType == "Text"
                    ? m.MessageText
                    : $"[{m.MessageType}]");

            return Ok(conversations.Select(c =>
            {
                var dto = MapToDto(c);
                dto.LastMessagePreview = previewByConversation.TryGetValue(c.Id, out var preview)
                    ? preview
                    : null;
                return dto;
            }));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ConversationDto>> GetById(Guid id)
        {
            var conversation = await _db.Conversations
                .Include(c => c.Customer)
                .Include(c => c.AssignedUser)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (conversation is null)
            {
                return NotFound(new { message = $"Conversation '{id}' was not found." });
            }

            return Ok(MapToDto(conversation));
        }

        [HttpGet("customer/{customerId:guid}")]
        public async Task<ActionResult<IEnumerable<ConversationDto>>> GetByCustomer(Guid customerId)
        {
            if (!await _db.Customers.AnyAsync(c => c.Id == customerId))
            {
                return NotFound(new { message = $"Customer '{customerId}' was not found." });
            }

            var conversations = await _db.Conversations
                .Include(c => c.Customer)
                .Include(c => c.AssignedUser)
                .Where(c => c.CustomerId == customerId)
                .OrderByDescending(c => c.LastMessageAt)
                .ToListAsync();

            return Ok(conversations.Select(MapToDto));
        }

        [HttpPut("{id:guid}/assign/{salesUserId:guid}")]
        public async Task<ActionResult<ConversationDto>> Assign(Guid id, Guid salesUserId)
        {
            var conversation = await _db.Conversations
                .Include(c => c.Customer)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (conversation is null)
            {
                return NotFound(new { message = $"Conversation '{id}' was not found." });
            }

            if (!await _db.SalesUsers.AnyAsync(u => u.Id == salesUserId))
            {
                return NotFound(new { message = $"Sales user '{salesUserId}' was not found." });
            }

            conversation.AssignedUserId = salesUserId;

            // Keep the customer's default owner in sync with the conversation owner.
            if (conversation.Customer is not null)
            {
                conversation.Customer.AssignedUserId = salesUserId;
            }

            await _db.SaveChangesAsync();

            await _db.Entry(conversation).Reference(c => c.AssignedUser).LoadAsync();

            return Ok(MapToDto(conversation));
        }

        [HttpPut("{id:guid}/close")]
        public async Task<ActionResult<ConversationDto>> Close(Guid id)
        {
            var conversation = await _db.Conversations
                .Include(c => c.Customer)
                .Include(c => c.AssignedUser)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (conversation is null)
            {
                return NotFound(new { message = $"Conversation '{id}' was not found." });
            }

            conversation.Status = "Closed";
            await _db.SaveChangesAsync();

            return Ok(MapToDto(conversation));
        }

        [HttpGet("{conversationId:guid}/messages")]
        public async Task<ActionResult<IEnumerable<ChatMessageDto>>> GetMessages(Guid conversationId)
        {
            if (!await _db.Conversations.AnyAsync(c => c.Id == conversationId))
            {
                return NotFound(new { message = $"Conversation '{conversationId}' was not found." });
            }

            var messages = await _db.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            return Ok(messages.Select(MapMessage));
        }

        private static ConversationDto MapToDto(Conversation c) => new()
        {
            Id = c.Id,
            CustomerId = c.CustomerId,
            CustomerName = c.Customer?.Name,
            CustomerWhatsAppNumber = c.Customer?.WhatsAppNumber,
            AssignedUserId = c.AssignedUserId,
            AssignedUserName = c.AssignedUser?.Name,
            Status = c.Status,
            CreatedAt = c.CreatedAt,
            LastMessageAt = c.LastMessageAt
        };

        private static ChatMessageDto MapMessage(ChatMessages m) => new()
        {
            Id = m.Id,
            ConversationId = m.ConversationId,
            Direction = m.Direction,
            MessageType = m.MessageType,
            MessageText = m.MessageText,
            MediaUrl = m.MediaUrl,
            WhatsAppMessageId = m.WhatsAppMessageId,
            CreatedAt = m.CreatedAt
        };
    }
}
