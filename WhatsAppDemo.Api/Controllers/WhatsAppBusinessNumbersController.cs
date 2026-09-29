using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppDemo.Api.Data;
using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Models;

namespace WhatsAppDemo.Api.Controllers
{
    /// <summary>
    /// CRM administration APIs for managing the WhatsApp Business numbers that map
    /// Meta PhoneNumberIds to sales people. Access tokens are never accepted or
    /// returned here; they remain server-side configuration.
    /// </summary>
    [ApiController]
    // Primary route matches the CRM admin spec; the hyphenated route is kept as an
    // alias so any existing clients continue to work.
    [Route("api/whatsapp/business-numbers")]
    [Route("api/whatsapp-business-numbers")]
    public class WhatsAppBusinessNumbersController : ControllerBase
    {
        private readonly AppDbContext _db;

        public WhatsAppBusinessNumbersController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<WhatsAppBusinessNumberDto>>> GetAll()
        {
            var numbers = await _db.WhatsAppBusinessNumbers
                .Include(n => n.SalesUser)
                .OrderBy(n => n.DisplayPhoneNumber)
                .ToListAsync();

            return Ok(numbers.Select(MapToDto));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<WhatsAppBusinessNumberDto>> GetById(Guid id)
        {
            var number = await _db.WhatsAppBusinessNumbers
                .Include(n => n.SalesUser)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (number is null)
            {
                return NotFound(new { message = $"WhatsApp business number '{id}' was not found." });
            }

            return Ok(MapToDto(number));
        }

        [HttpPost]
        public async Task<ActionResult<WhatsAppBusinessNumberDto>> Create(
            [FromBody] CreateWhatsAppBusinessNumberRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(request.PhoneNumberId))
            {
                return BadRequest(new { message = "PhoneNumberId is required." });
            }

            if (!await _db.SalesUsers.AnyAsync(u => u.Id == request.SalesUserId))
            {
                return BadRequest(new { message = $"Sales user '{request.SalesUserId}' was not found." });
            }

            // Prevent duplicate active PhoneNumberId so incoming webhooks always map
            // to exactly one business number.
            var duplicate = await _db.WhatsAppBusinessNumbers
                .AnyAsync(n => n.PhoneNumberId == request.PhoneNumberId && n.IsActive && request.IsActive);

            if (duplicate)
            {
                return Conflict(new
                {
                    message = $"An active WhatsApp business number with PhoneNumberId '{request.PhoneNumberId}' already exists."
                });
            }

            var number = new WhatsAppBusinessNumber
            {
                Id = Guid.NewGuid(),
                SalesUserId = request.SalesUserId,
                PhoneNumberId = request.PhoneNumberId,
                DisplayPhoneNumber = request.DisplayPhoneNumber,
                WabaId = request.WabaId,
                AccessToken = string.IsNullOrWhiteSpace(request.AccessToken) ? null : request.AccessToken,
                IsTestNumber = request.IsTestNumber,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _db.WhatsAppBusinessNumbers.Add(number);
            await _db.SaveChangesAsync();

            await _db.Entry(number).Reference(n => n.SalesUser).LoadAsync();

            return CreatedAtAction(nameof(GetById), new { id = number.Id }, MapToDto(number));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<WhatsAppBusinessNumberDto>> Update(
            Guid id,
            [FromBody] UpdateWhatsAppBusinessNumberRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var number = await _db.WhatsAppBusinessNumbers
                .FirstOrDefaultAsync(n => n.Id == id);

            if (number is null)
            {
                return NotFound(new { message = $"WhatsApp business number '{id}' was not found." });
            }

            if (string.IsNullOrWhiteSpace(request.PhoneNumberId))
            {
                return BadRequest(new { message = "PhoneNumberId is required." });
            }

            if (!await _db.SalesUsers.AnyAsync(u => u.Id == request.SalesUserId))
            {
                return BadRequest(new { message = $"Sales user '{request.SalesUserId}' was not found." });
            }

            var duplicate = await _db.WhatsAppBusinessNumbers
                .AnyAsync(n => n.Id != id &&
                               n.PhoneNumberId == request.PhoneNumberId &&
                               n.IsActive && request.IsActive);

            if (duplicate)
            {
                return Conflict(new
                {
                    message = $"An active WhatsApp business number with PhoneNumberId '{request.PhoneNumberId}' already exists."
                });
            }

            number.SalesUserId = request.SalesUserId;
            number.PhoneNumberId = request.PhoneNumberId;
            number.DisplayPhoneNumber = request.DisplayPhoneNumber;
            number.WabaId = request.WabaId;
            // Only overwrite the stored token when a new one is supplied; otherwise keep
            // the existing secret untouched.
            if (!string.IsNullOrWhiteSpace(request.AccessToken))
            {
                number.AccessToken = request.AccessToken;
            }
            number.IsTestNumber = request.IsTestNumber;
            number.IsActive = request.IsActive;
            number.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _db.Entry(number).Reference(n => n.SalesUser).LoadAsync();

            return Ok(MapToDto(number));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var number = await _db.WhatsAppBusinessNumbers
                .FirstOrDefaultAsync(n => n.Id == id);

            if (number is null)
            {
                return NotFound(new { message = $"WhatsApp business number '{id}' was not found." });
            }

            _db.WhatsAppBusinessNumbers.Remove(number);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        private static WhatsAppBusinessNumberDto MapToDto(WhatsAppBusinessNumber n) => new()
        {
            Id = n.Id,
            SalesUserId = n.SalesUserId,
            SalesUserName = n.SalesUser?.Name,
            PhoneNumberId = n.PhoneNumberId,
            DisplayPhoneNumber = n.DisplayPhoneNumber,
            WabaId = n.WabaId,
            IsActive = n.IsActive,
            IsTestNumber = n.IsTestNumber,
            CreatedAt = n.CreatedAt,
            UpdatedAt = n.UpdatedAt
        };
    }
}
