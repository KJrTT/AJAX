using AutoMapper;


namespace MyApi;

/// <summary>
/// Реализация бизнес-логики
/// </summary>
public class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly IMapper _mapper;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IProductRepository repository,
        IMapper mapper,
        ILogger<ProductService> logger)
    {
        _repository = repository;
        _mapper = mapper;
        _logger = logger;
    }

    // ============ V1 МЕТОДЫ ============

    public async Task<IEnumerable<ProductResponseV1>> GetAllV1Async()
    {
        _logger.LogInformation("Запрос всех продуктов (V1)");
        
        var entities = await _repository.GetAllAsync();
        return _mapper.Map<IEnumerable<ProductResponseV1>>(entities);
    }

    public async Task<ProductResponseV1?> GetByIdV1Async(Guid id)
    {
        _logger.LogInformation("Запрос продукта (V1) с ID: {Id}", id);
        
        var entity = await _repository.GetByIdAsync(id);
        return entity == null ? null : _mapper.Map<ProductResponseV1>(entity);
    }

    public async Task<ProductResponseV1> CreateV1Async(ProductRequestV1 request)
    {
        _logger.LogInformation("Создание продукта (V1): {Name}", request.Name);
        
        var entity = _mapper.Map<ProductEntity>(request);
        var created = await _repository.CreateAsync(entity);
        
        return _mapper.Map<ProductResponseV1>(created);
    }

    public async Task<ProductResponseV1?> UpdateV1Async(Guid id, ProductRequestV1 request)
    {
        _logger.LogInformation("Обновление продукта (V1) с ID: {Id}", id);
        
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return null;

        // Обновляем только разрешенные поля для V1
        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.PriceInCents = (int)(request.Price * 100); // Конвертация в центы
        existing.Category = request.Category;
        existing.StockQuantity = request.Stock;
        existing.Attributes = request.Attributes;

        var updated = await _repository.UpdateAsync(existing);
        return _mapper.Map<ProductResponseV1>(updated);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        _logger.LogInformation("Удаление продукта с ID: {Id}", id);
        return await _repository.DeleteAsync(id);
    }

    // ============ V2 МЕТОДЫ ============

    public async Task<IEnumerable<ProductResponseV2>> GetAllV2Async()
    {
        _logger.LogInformation("Запрос всех продуктов (V2)");
        
        var entities = await _repository.GetAllAsync();
        return _mapper.Map<IEnumerable<ProductResponseV2>>(entities);
    }

    public async Task<ProductResponseV2?> GetByIdV2Async(Guid id)
    {
        _logger.LogInformation("Запрос продукта (V2) с ID: {Id}", id);
        
        var entity = await _repository.GetByIdAsync(id);
        return entity == null ? null : _mapper.Map<ProductResponseV2>(entity);
    }

    public async Task<ProductResponseV2> CreateV2Async(ProductRequestV1 request)
    {
        _logger.LogInformation("Создание продукта (V2): {Name}", request.Name);
        
        var entity = _mapper.Map<ProductEntity>(request);
        var created = await _repository.CreateAsync(entity);
        
        return _mapper.Map<ProductResponseV2>(created);
    }
}