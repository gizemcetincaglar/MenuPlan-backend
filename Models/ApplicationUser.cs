using Microsoft.AspNetCore.Identity;

namespace Menulux.Api.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    // Null means the user is a super admin (manages all restaurants).
    public Guid? RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }
}
