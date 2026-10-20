using System.Security.Cryptography;
using System.Text;

namespace Samost.Services;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }

    public string GetETag()
    {
        var raw = $"{Id}-{Name}-{Price}-{CategoryId}";
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = MD5.HashData(bytes);
        return $"\"{Convert.ToHexString(hash).ToLower()}\"";
    }
}