namespace Menulux.Api.DTOs;

public record CategoryDto(Guid Id, string Name, int SortOrder);
public record CreateCategoryRequest(string Name, int SortOrder);
public record UpdateCategoryRequest(string Name, int SortOrder);

public record ProductDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    bool IsAvailable,
    int SortOrder);

public record CreateProductRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    bool IsAvailable,
    int SortOrder);

public record UpdateProductRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    bool IsAvailable,
    int SortOrder);

public record PublicMenuResponse(string RestaurantName, IEnumerable<PublicCategoryDto> Categories);
public record PublicCategoryDto(string Name, int SortOrder, IEnumerable<ProductDto> Products);

public record PublicTableDto(string Name, string? Zone);
