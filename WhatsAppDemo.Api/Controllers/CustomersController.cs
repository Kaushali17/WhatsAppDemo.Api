using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppDemo.Api.Data;
using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Models;

namespace WhatsAppDemo.Api.Controllers
{
    [ApiController]
    [Route("api/customers")]
    public class CustomersController : ControllerBase
    {
        private readonly AppDbContext _db;

        public CustomersController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerDto>>> GetAll()
        {
            var customers = await _db.Customers
                .Include(c => c.AssignedUser)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return Ok(customers.Select(MapToDto));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<CustomerDto>> GetById(Guid id)
        {
            var customer = await _db.Customers
                .Include(c => c.AssignedUser)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer is null)
            {
                return NotFound(new { message = $"Customer '{id}' was not found." });
            }

            return Ok(MapToDto(customer));
        }

        [HttpPost]
        public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.AssignedUserId.HasValue &&
                !await _db.SalesUsers.AnyAsync(u => u.Id == request.AssignedUserId.Value))
            {
                return BadRequest(new { message = $"Sales user '{request.AssignedUserId}' was not found." });
            }

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                WhatsAppNumber = request.WhatsAppNumber.Trim(),
                AssignedUserId = request.AssignedUserId,
                CreatedAt = DateTime.UtcNow
            };

            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();

            await _db.Entry(customer).Reference(c => c.AssignedUser).LoadAsync();

            return CreatedAtAction(nameof(GetById), new { id = customer.Id }, MapToDto(customer));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<CustomerDto>> Update(Guid id, [FromBody] UpdateCustomerRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id);

            if (customer is null)
            {
                return NotFound(new { message = $"Customer '{id}' was not found." });
            }

            if (request.AssignedUserId.HasValue &&
                !await _db.SalesUsers.AnyAsync(u => u.Id == request.AssignedUserId.Value))
            {
                return BadRequest(new { message = $"Sales user '{request.AssignedUserId}' was not found." });
            }

            customer.Name = request.Name;
            customer.WhatsAppNumber = request.WhatsAppNumber.Trim();
            customer.AssignedUserId = request.AssignedUserId;

            await _db.SaveChangesAsync();

            await _db.Entry(customer).Reference(c => c.AssignedUser).LoadAsync();

            return Ok(MapToDto(customer));
        }

        private static CustomerDto MapToDto(Customer c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            WhatsAppNumber = c.WhatsAppNumber,
            AssignedUserId = c.AssignedUserId,
            AssignedUserName = c.AssignedUser?.Name,
            CreatedAt = c.CreatedAt
        };
    }
}
