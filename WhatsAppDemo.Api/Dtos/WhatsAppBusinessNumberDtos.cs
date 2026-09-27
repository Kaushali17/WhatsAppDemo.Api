using System.ComponentModel.DataAnnotations;

namespace WhatsAppDemo.Api.Dtos
{
    /// <summary>
    /// Safe representation of a WhatsApp Business number. Never exposes any access
    /// token, app secret or webhook secret.
    /// </summary>
    public class WhatsAppBusinessNumberDto
    {
        public Guid Id { get; set; }

        public Guid SalesUserId { get; set; }

        public string? SalesUserName { get; set; }

        public string PhoneNumberId { get; set; } = string.Empty;

        public string DisplayPhoneNumber { get; set; } = string.Empty;

        public string? WabaId { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateWhatsAppBusinessNumberRequest
    {
        [Required]
        public Guid SalesUserId { get; set; }

        [Required]
        public string PhoneNumberId { get; set; } = string.Empty;

        [Required]
        public string DisplayPhoneNumber { get; set; } = string.Empty;

        public string? WabaId { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class UpdateWhatsAppBusinessNumberRequest
    {
        [Required]
        public Guid SalesUserId { get; set; }

        [Required]
        public string PhoneNumberId { get; set; } = string.Empty;

        [Required]
        public string DisplayPhoneNumber { get; set; } = string.Empty;

        public string? WabaId { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
