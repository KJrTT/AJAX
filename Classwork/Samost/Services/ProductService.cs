using Samost.DTO;

namespace Samost.Services;

public class ProductService
{
    private static readonly List<Product> _products = new();
    private static int _nextId = 1;

    public IReadOnlyList<Product> GetAll() => _products;

    public Product? GetById(int id) => _products.FirstOrDefault(p => p.Id == id);

    public Product Add(CreateProductDto dto)
    {
        var product = new Product
        {
            Id = _nextId++,
            Name = dto.Name,
            Price = dto.Price,
            CategoryId = dto.CategoryId
        };
        _products.Add(product);
        return product;
    }
}