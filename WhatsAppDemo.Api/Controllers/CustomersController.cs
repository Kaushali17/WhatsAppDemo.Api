using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppDemo.Api.Data;
using WhatsAppDemo.Api.Dtos;
using WhatsAppDemo.Api.Models;
using WhatsAppDemo.Api.Services;

namespace WhatsAppDemo.Api.Controllers
{
    [ApiController]
    [Route("api/customers")]
    public class CustomersController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IUserAccessResolver _accessResolver;

        public CustomersController(AppDbContext db, IUserAccessResolver accessResolver)
        {
            _db = db;
            _accessResolver = accessResolver;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerDto>>> GetAll([FromQuery] Guid? salesUserId = null)
        {
            var scope = await _accessResolver.ResolveAsync(salesUserId);

            var query = _db.Customers
                .Include(c => c.AssignedUser)
                .AsQueryable();

            // Salesperson users only see customers they own (directly assigned or via
            // one of their conversations); Admin (and legacy unscoped) see all.
            if (!scope.CanSeeAll)
            {
                query = query.Where(c =>
                    c.AssignedUserId == scope.SalesUserId ||
                    c.Conversations.Any(conv => conv.AssignedUserId == scope.SalesUserId));
            }

            var customers = await query
                .OrderBy(c => c.Name)
                .ToListAsync();

            return Ok(customers.Select(MapToDto));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<CustomerDto>> GetById(Guid id, [FromQuery] Guid? salesUserId = null)
        {
            var customer = await _db.Customers
                .Include(c => c.AssignedUser)
                .Include(c => c.Conversations)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer is null)
            {
                return NotFound(new { message = $"Customer '{id}' was not found." });
            }

            var scope = await _accessResolver.ResolveAsync(salesUserId);
            if (!scope.CanSeeAll &&
                customer.AssignedUserId != scope.SalesUserId &&
                !customer.Conversations.Any(conv => conv.AssignedUserId == scope.SalesUserId))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "You do not have access to this customer." });
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
