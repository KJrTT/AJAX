namespace WebApplication3.Domain
{

    public class Customer
    {
        public Guid Id { get; private set; }
        public string Email { get; private set; } = default!;
        public string Name { get; private set; } = default!;
        public bool IsBlocked { get; private set; }

        private Customer() { }

        public Customer(string email, string name)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email required", nameof(email));

            Id = Guid.NewGuid();
            Email = email.Trim().ToLowerInvariant();
            Name = name.Trim();
        }

        public void Block() => IsBlocked = true;
        public void Unblock() => IsBlocked = false;
    }
}
