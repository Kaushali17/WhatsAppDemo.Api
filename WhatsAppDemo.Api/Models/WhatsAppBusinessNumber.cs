namespace WhatsAppDemo.Api.Models
{
    /// <summary>
    /// A real WhatsApp Business number registered in the CRM. Each number maps a
    /// Meta PhoneNumberId to a sales person, allowing the CRM to route incoming and
    /// outgoing messages through the correct business number. A sales person can
    /// own more than one number.
    /// </summary>
    public class WhatsAppBusinessNumber
    {
        public Guid Id { get; set; }

        public Guid SalesUserId { get; set; }

        /// <summary>
        /// Meta's phone_number_id used in the Cloud API URL for sending messages and
        /// found in the incoming webhook payload under metadata.phone_number_id.
        /// </summary>
        public string PhoneNumberId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable display number, e.g. "+9199XXXXXXXX".
        /// </summary>
        public string DisplayPhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// Optional WhatsApp Business Account id.
        /// </summary>
        public string? WabaId { get; set; }

        /// <summary>
        /// Optional per-number Meta Cloud API access token. When null, the globally
        /// configured token (WhatsApp:AccessToken) is used. Never exposed through any
        /// read API/DTO. Prefer supplying this via a secret store in production.
        /// </summary>
        public string? AccessToken { get; set; }

        /// <summary>
        /// True for a Meta Developer test number (as opposed to a real, verified
        /// business number). Purely informational for the CRM.
        /// </summary>
        public bool IsTestNumber { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public SalesUser? SalesUser { get; set; }

        public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    }
}
