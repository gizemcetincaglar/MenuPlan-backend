using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Menulux.Api.Hubs;

[Authorize(Roles = "RestaurantAdmin,Kitchen,Waiter,Cashier")]
public class OrderHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var restaurantId = Context.User?.FindFirst("restaurantId")?.Value;
        if (!string.IsNullOrEmpty(restaurantId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, restaurantId);
        }

        await base.OnConnectedAsync();
    }
}
