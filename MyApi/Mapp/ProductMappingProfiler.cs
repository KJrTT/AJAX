using AutoMapper;


namespace MyApi;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // ============ Entity -> V1 ============
        CreateMap<ProductEntity, ProductResponseV1>()
            .ForMember(dest => dest.Price, opt => opt.MapFrom(src => 
                $"{src.PriceInCents / 100m:F2} $"))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => 
                src.DeletedAt == null));

        // ============ Entity -> V2 ============
        CreateMap<ProductEntity, ProductResponseV2>()
            .ForMember(dest => dest.Price, opt => opt.MapFrom(src => 
                src.PriceInCents / 100m))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => 
                src.DeletedAt == null));

        // ============ Request -> Entity ============
        CreateMap<ProductRequest, ProductEntity>()
            .ForMember(dest => dest.PriceInCents, opt => opt.MapFrom(src => 
                (int)(src.Price * 100)))
            .ForMember(dest => dest.StockQuantity, opt => opt.MapFrom(src => 
                src.Stock))
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore());
    }
}