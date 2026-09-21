using Menulux.Api.Data;
using Menulux.Api.DTOs;
using Menulux.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Menulux.Api.Controllers;

[Authorize(Roles = "SuperAdmin")]
[ApiController]
[Route("api/restaurants")]
public class RestaurantsController : ControllerBase
{
    private readonly AppDbContext _db;

    public RestaurantsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RestaurantDto>>> GetAll()
    {
        var restaurants = await _db.Restaurants
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RestaurantDto(
                r.Id,
                r.Name,
                r.Slug,
                r.CreatedAt,
                _db.Users.Count(u => u.RestaurantId == r.Id)))
            .ToListAsync();

        return Ok(restaurants);
    }

    [HttpPost]
    public async Task<ActionResult<RestaurantDto>> Create(CreateRestaurantRequest request)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await _db.Restaurants.AnyAsync(r => r.Slug == slug))
        {
            return BadRequest(new { message = "Bu slug zaten kullanılıyor." });
        }

        var restaurant = new Restaurant { Name = request.Name.Trim(), Slug = slug };
        _db.Restaurants.Add(restaurant);
        await _db.SaveChangesAsync();

        return Ok(new RestaurantDto(restaurant.Id, restaurant.Name, restaurant.Slug, restaurant.CreatedAt, 0));
    }

    [HttpGet("{id:guid}/admins")]
    public async Task<ActionResult<IEnumerable<RestaurantAdminDto>>> GetAdmins(Guid id)
    {
        var admins = await _db.Users
            .Where(u => u.RestaurantId == id)
            .Select(u => new RestaurantAdminDto(u.Id, u.Email!, u.FullName))
            .ToListAsync();

        return Ok(admins);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var restaurant = await _db.Restaurants.FindAsync(id);
        if (restaurant is null) return NotFound();

        _db.Restaurants.Remove(restaurant);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
