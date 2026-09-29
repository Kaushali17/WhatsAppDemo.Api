using System.ComponentModel.DataAnnotations;

namespace WhatsAppDemo.Api.Dtos
{
    public class SalesUserDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// CRM role: "Admin" or "Salesperson". Admin users can see all CRM data.
        /// </summary>
        public string Role { get; set; } = "Salesperson";

        /// <summary>
        /// The sales person's WhatsApp business numbers (safe fields only; never
        /// includes any access token).
        /// </summary>
        public List<SalesUserWhatsAppNumberDto> WhatsAppBusinessNumbers { get; set; } = new();
    }

    public class SalesUserWhatsAppNumberDto
    {
        public Guid Id { get; set; }

        public string DisplayPhoneNumber { get; set; } = string.Empty;

        public string PhoneNumberId { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }

    public class CreateSalesUserRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
