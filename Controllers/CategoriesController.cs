using Menulux.Api.Data;
using Menulux.Api.DTOs;
using Menulux.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Menulux.Api.Controllers;

[Authorize(Roles = "RestaurantAdmin,Waiter,Cashier")]
[ApiController]
[Route("api/categories")]
public class CategoriesController : RestaurantScopedController
{
    private readonly AppDbContext _db;

    public CategoriesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAll()
    {
        var restaurantId = GetRestaurantId();
        if (restaurantId is null) return Forbid();

        var categories = await _db.Categories
            .Where(c => c.RestaurantId == restaurantId)
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto(c.Id, c.Name, c.SortOrder))
            .ToListAsync();

        return Ok(categories);
    }

    [Authorize(Roles = "RestaurantAdmin")]
    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryRequest request)
    {
        var restaurantId = GetRestaurantId();
        if (restaurantId is null) return Forbid();

        var category = new Category
        {
            RestaurantId = restaurantId.Value,
            Name = request.Name,
            SortOrder = request.SortOrder,
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return Ok(new CategoryDto(category.Id, category.Name, category.SortOrder));
    }

    [Authorize(Roles = "RestaurantAdmin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCategoryRequest request)
    {
        var restaurantId = GetRestaurantId();
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.RestaurantId == restaurantId);
        if (category is null) return NotFound();

        category.Name = request.Name;
        category.SortOrder = request.SortOrder;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "RestaurantAdmin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var restaurantId = GetRestaurantId();
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.RestaurantId == restaurantId);
        if (category is null) return NotFound();

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
