using Microsoft.EntityFrameworkCore;
using WhatsAppDemo.Api.Data;

namespace WhatsAppDemo.Api.Services
{
    /// <summary>
    /// Resolves the CRM data-access scope for a request based on the acting
    /// <c>SalesUserId</c>. This layers role-based authorization (Admin vs
    /// Salesperson) on top of the existing SalesUserId identity mechanism without
    /// introducing a second authentication system.
    /// </summary>
    public interface IUserAccessResolver
    {
        Task<UserAccessScope> ResolveAsync(Guid? salesUserId);
    }

    /// <summary>
    /// Describes what CRM data the acting user may see.
    /// </summary>
    public sealed class UserAccessScope
    {
        /// <summary>The acting user, or null when no (or an unknown) user was supplied.</summary>
        public Guid? SalesUserId { get; init; }

        /// <summary>The acting user's role ("Admin" or "Salesperson").</summary>
        public string Role { get; init; } = "Salesperson";

        /// <summary>
        /// True when the caller can see all salespeople's data. Admin users, and
        /// (for backward compatibility) requests that do not supply a SalesUserId,
        /// see everything so existing API contracts keep working.
        /// </summary>
        public bool CanSeeAll { get; init; }

        public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    public sealed class UserAccessResolver : IUserAccessResolver
    {
        private readonly AppDbContext _db;

        public UserAccessResolver(AppDbContext db)
        {
            _db = db;
        }

        public async Task<UserAccessScope> ResolveAsync(Guid? salesUserId)
        {
            // Backward-compatible: no acting user means no scoping (existing behaviour).
            if (salesUserId is not Guid userId)
            {
                return new UserAccessScope { CanSeeAll = true };
            }

            var user = await _db.SalesUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
            {
                // Unknown user: fall back to the legacy unscoped behaviour.
                return new UserAccessScope { CanSeeAll = true };
            }

            var isAdmin = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase);

            return new UserAccessScope
            {
                SalesUserId = user.Id,
                Role = user.Role,
                CanSeeAll = isAdmin
            };
        }
    }
}
