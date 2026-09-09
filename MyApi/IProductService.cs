namespace MyApi;

/// <summary>
/// Бизнес-логика для работы с продуктами
/// </summary>
public interface IProductService
{
    // V1 методы
    Task<IEnumerable<ProductResponseV1>> GetAllV1Async();
    Task<ProductResponseV1?> GetByIdV1Async(Guid id);
    Task<ProductResponseV1> CreateV1Async(ProductRequestV1 request);
    Task<ProductResponseV1?> UpdateV1Async(Guid id, ProductRequestV1 request);
    Task<bool> DeleteAsync(Guid id);

    // V2 методы (расширенные)
    Task<IEnumerable<ProductResponseV2>> GetAllV2Async();
    Task<ProductResponseV2?> GetByIdV2Async(Guid id);
    Task<ProductResponseV2> CreateV2Async(ProductRequest request);
}