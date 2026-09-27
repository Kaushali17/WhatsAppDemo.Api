using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppDemo.Api.Data;
using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Models;

namespace WhatsAppDemo.Api.Controllers
{
    [ApiController]
    [Route("api/salesusers")]
    public class SalesUsersController : ControllerBase
    {
        private readonly AppDbContext _db;

        public SalesUsersController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SalesUserDto>>> GetAll()
        {
            var users = await _db.SalesUsers
                .Include(u => u.WhatsAppBusinessNumbers)
                .OrderBy(u => u.Name)
                .ToListAsync();

            return Ok(users.Select(MapToDto));
        }

        [HttpPost]
        public async Task<ActionResult<SalesUserDto>> Create([FromBody] CreateSalesUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = new SalesUser
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Email = request.Email
            };

            _db.SalesUsers.Add(user);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAll), new { id = user.Id }, MapToDto(user));
        }

        private static SalesUserDto MapToDto(SalesUser u) => new()
        {
            Id = u.Id,
            Name = u.Name,
            Email = u.Email,
            WhatsAppBusinessNumbers = u.WhatsAppBusinessNumbers
                .Select(n => new SalesUserWhatsAppNumberDto
                {
                    Id = n.Id,
                    DisplayPhoneNumber = n.DisplayPhoneNumber,
                    PhoneNumberId = n.PhoneNumberId,
                    IsActive = n.IsActive
                })
                .ToList()
        };
    }
}
