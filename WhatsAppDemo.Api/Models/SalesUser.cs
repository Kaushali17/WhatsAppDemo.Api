namespace WhatsAppDemo.Api.Models
{
    public class SalesUser
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// CRM role that controls how much data the user can see. "Admin" users can
        /// see all salespeople, customers and conversations; "Salesperson" users can
        /// only see their own. This is a CRM permission only and never changes which
        /// WhatsApp Business number is used to send/receive messages.
        /// </summary>
        public string Role { get; set; } = "Salesperson";

        // Navigation properties
        public ICollection<Customer> Customers { get; set; } = new List<Customer>();

        public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

        public ICollection<WhatsAppBusinessNumber> WhatsAppBusinessNumbers { get; set; } = new List<WhatsAppBusinessNumber>();
    }
}
