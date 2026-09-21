using Menulux.Api.Data;
using Menulux.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Menulux.Api.Controllers;

[Authorize(Roles = "RestaurantAdmin")]
[ApiController]
[Route("api/restaurants/mine")]
public class MyRestaurantController : RestaurantScopedController
{
    private readonly AppDbContext _db;

    public MyRestaurantController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<RestaurantDto>> Get()
    {
        var restaurantId = GetRestaurantId();
        if (restaurantId is null) return Forbid();

        var restaurant = await _db.Restaurants.FirstOrDefaultAsync(r => r.Id == restaurantId);
        if (restaurant is null) return NotFound();

        var adminCount = await _db.Users.CountAsync(u => u.RestaurantId == restaurant.Id);
        return Ok(new RestaurantDto(restaurant.Id, restaurant.Name, restaurant.Slug, restaurant.CreatedAt, adminCount));
    }
}
