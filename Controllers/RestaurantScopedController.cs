using Microsoft.AspNetCore.Mvc;

namespace Menulux.Api.Controllers;

public abstract class RestaurantScopedController : ControllerBase
{
    protected Guid? GetRestaurantId()
    {
        var claim = User.FindFirst("restaurantId")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
