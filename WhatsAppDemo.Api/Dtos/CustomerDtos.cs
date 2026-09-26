using System.ComponentModel.DataAnnotations;

namespace WhatsAppDemo.Api.Dtos
{
    public class CustomerDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string WhatsAppNumber { get; set; } = string.Empty;

        public Guid? AssignedUserId { get; set; }

        public string? AssignedUserName { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class CreateCustomerRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string WhatsAppNumber { get; set; } = string.Empty;

        public Guid? AssignedUserId { get; set; }
    }

    public class UpdateCustomerRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string WhatsAppNumber { get; set; } = string.Empty;

        public Guid? AssignedUserId { get; set; }
    }
}
