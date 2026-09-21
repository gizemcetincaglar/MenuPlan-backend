namespace Menulux.Api.DTOs;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, string Email, string FullName, Guid? RestaurantId, IEnumerable<string> Roles);

public record RegisterRestaurantAdminRequest(string Email, string Password, string FullName, Guid RestaurantId);

public record RegisterStaffRequest(string Email, string Password, string FullName, string Role);
