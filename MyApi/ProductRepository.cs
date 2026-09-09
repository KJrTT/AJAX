using Microsoft.EntityFrameworkCore;
using MyApi;

namespace MyApi;

/// <summary>
/// Реализация репозитория с Entity Framework Core
/// </summary>
public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ProductEntity>> GetAllAsync(bool includeDeleted = false)
    {
        var query = _context.Products.AsQueryable();

        if (!includeDeleted)
        {
            query = query.Where(p => p.DeletedAt == null);
        }

        return await query.ToListAsync();
    }

    public async Task<ProductEntity?> GetByIdAsync(Guid id, bool includeDeleted = false)
    {
        var query = _context.Products.AsQueryable();

        if (!includeDeleted)
        {
            query = query.Where(p => p.DeletedAt == null);
        }

        return await query.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<ProductEntity>> GetByCategoryAsync(string category)
    {
        return await _context.Products
            .Where(p => p.Category == category && p.DeletedAt == null)
            .ToListAsync();
    }

    public async Task<ProductEntity> CreateAsync(ProductEntity entity)
    {
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTime.UtcNow;

        await _context.Products.AddAsync(entity);
        await _context.SaveChangesAsync();

        return entity;
    }

    public async Task<ProductEntity> UpdateAsync(ProductEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;

        _context.Products.Update(entity);
        await _context.SaveChangesAsync();

        return entity;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity == null)
            return false;

        entity.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Products
            .AnyAsync(p => p.Id == id && p.DeletedAt == null);
    }

    public async Task<int> CountAsync()
    {
        return await _context.Products
            .CountAsync(p => p.DeletedAt == null);
    }
}