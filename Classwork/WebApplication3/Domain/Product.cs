namespace WebApplication3.Domain
{


    public class Product
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; } = default!;
        public decimal Price { get; private set; }
        public int Stock { get; private set; }

        private Product() { }

        public Product(string name, decimal price, int stock)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name required", nameof(name));
            if (price <= 0)
                throw new ArgumentException("Price must be positive", nameof(price));
            if (stock < 0)
                throw new ArgumentException("Stock cannot be negative", nameof(stock));

            Id = Guid.NewGuid();
            Name = name.Trim();
            Price = price;
            Stock = stock;
        }

        public void DecreaseStock(int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be positive", nameof(quantity));
            if (Stock < quantity)
                throw new InvalidOperationException($"Not enough stock for '{Name}'");
            Stock -= quantity;
        }

        public void IncreaseStock(int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be positive", nameof(quantity));
            Stock += quantity;
        }
    }
}
