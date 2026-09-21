using Menulux.Api.Data;
using Menulux.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Menulux.Api.Controllers;

[ApiController]
[Route("api/public/menu")]
public class PublicMenuController : ControllerBase
{
    private readonly AppDbContext _db;

    public PublicMenuController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<PublicMenuResponse>> GetBySlug(string slug)
    {
        var restaurant = await _db.Restaurants
            .Include(r => r.Categories)
            .ThenInclude(c => c.Products)
            .FirstOrDefaultAsync(r => r.Slug == slug);

        if (restaurant is null) return NotFound();

        var categories = restaurant.Categories
            .OrderBy(c => c.SortOrder)
            .Select(c => new PublicCategoryDto(
                c.Name,
                c.SortOrder,
                c.Products
                    .Where(p => p.IsAvailable)
                    .OrderBy(p => p.SortOrder)
                    .Select(p => new ProductDto(p.Id, p.CategoryId, p.Name, p.Description, p.Price, p.ImageUrl, p.IsAvailable, p.SortOrder))));

        return Ok(new PublicMenuResponse(restaurant.Name, categories));
    }

    [HttpGet("{slug}/tables")]
    public async Task<ActionResult<IEnumerable<PublicTableDto>>> GetTables(string slug)
    {
        var restaurant = await _db.Restaurants.FirstOrDefaultAsync(r => r.Slug == slug);
        if (restaurant is null) return NotFound();

        var tables = await _db.Tables
            .Where(t => t.RestaurantId == restaurant.Id)
            .OrderBy(t => t.Name)
            .Select(t => new PublicTableDto(t.Name, t.Zone))
            .ToListAsync();

        return Ok(tables);
    }
}
