namespace MyApi;

/// <summary>
/// Интерфейс репозитория продуктов
/// </summary>
public interface IProductRepository
{
    Task<IEnumerable<ProductEntity>> GetAllAsync(bool includeDeleted = false);
    Task<ProductEntity?> GetByIdAsync(Guid id, bool includeDeleted = false);
    Task<IEnumerable<ProductEntity>> GetByCategoryAsync(string category);
    Task<ProductEntity> CreateAsync(ProductEntity entity);
    Task<ProductEntity> UpdateAsync(ProductEntity entity);
    Task<bool> DeleteAsync(Guid id); // Soft delete
    Task<bool> ExistsAsync(Guid id);
    Task<int> CountAsync();
}