using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppDemo.Api.Data;
using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Models;
using WhatsAppDemo.Api.Services;

namespace WhatsAppDemo.Api.Controllers
{
    [ApiController]
    [Route("api/conversations")]
    public class ConversationsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IUserAccessResolver _accessResolver;

        public ConversationsController(AppDbContext db, IUserAccessResolver accessResolver)
        {
            _db = db;
            _accessResolver = accessResolver;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ConversationDto>>> GetAll([FromQuery] Guid? salesUserId = null)
        {
            var scope = await _accessResolver.ResolveAsync(salesUserId);

            var query = _db.Conversations
                .Include(c => c.Customer)
                .Include(c => c.AssignedUser)
                .Include(c => c.WhatsAppBusinessNumber)
                .AsQueryable();

            // Salesperson users only ever see their own conversations; Admin (and the
            // legacy unscoped path) see all.
            if (!scope.CanSeeAll)
            {
                query = query.Where(c => c.AssignedUserId == scope.SalesUserId);
            }

            var conversations = await query
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
        public async Task<ActionResult<ConversationDto>> GetById(Guid id, [FromQuery] Guid? salesUserId = null)
        {
            var conversation = await _db.Conversations
                .Include(c => c.Customer)
                .Include(c => c.AssignedUser)
                .Include(c => c.WhatsAppBusinessNumber)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (conversation is null)
            {
                return NotFound(new { message = $"Conversation '{id}' was not found." });
            }

            var scope = await _accessResolver.ResolveAsync(salesUserId);
            if (!scope.CanSeeAll && conversation.AssignedUserId != scope.SalesUserId)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "You do not have access to this conversation." });
            }

            return Ok(MapToDto(conversation));
        }

        [HttpGet("customer/{customerId:guid}")]
        public async Task<ActionResult<IEnumerable<ConversationDto>>> GetByCustomer(Guid customerId, [FromQuery] Guid? salesUserId = null)
        {
            if (!await _db.Customers.AnyAsync(c => c.Id == customerId))
            {
                return NotFound(new { message = $"Customer '{customerId}' was not found." });
            }

            var scope = await _accessResolver.ResolveAsync(salesUserId);

            var query = _db.Conversations
                .Include(c => c.Customer)
                .Include(c => c.AssignedUser)
                .Include(c => c.WhatsAppBusinessNumber)
                .Where(c => c.CustomerId == customerId);

            if (!scope.CanSeeAll)
            {
                query = query.Where(c => c.AssignedUserId == scope.SalesUserId);
            }

            var conversations = await query
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
                .Include(c => c.WhatsAppBusinessNumber)
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
        public async Task<ActionResult<IEnumerable<ChatMessageDto>>> GetMessages(Guid conversationId, [FromQuery] Guid? salesUserId = null)
        {
            var conversation = await _db.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation is null)
            {
                return NotFound(new { message = $"Conversation '{conversationId}' was not found." });
            }

            var scope = await _accessResolver.ResolveAsync(salesUserId);
            if (!scope.CanSeeAll && conversation.AssignedUserId != scope.SalesUserId)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "You do not have access to this conversation." });
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
            WhatsAppBusinessNumberId = c.WhatsAppBusinessNumberId,
            WhatsAppDisplayPhoneNumber = c.WhatsAppBusinessNumber?.DisplayPhoneNumber,
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
