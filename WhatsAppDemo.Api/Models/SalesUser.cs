namespace WhatsAppDemo.Api.Models
{
    public class SalesUser
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        // Navigation properties
        public ICollection<Customer> Customers { get; set; } = new List<Customer>();

        public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    }
}
