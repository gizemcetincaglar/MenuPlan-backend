namespace Menulux.Api.DTOs;

public record RestaurantDto(Guid Id, string Name, string Slug, DateTime CreatedAt, int AdminCount);
public record CreateRestaurantRequest(string Name, string Slug);
public record RestaurantAdminDto(Guid Id, string Email, string FullName);
