using Menulux.Api.Data;
using Menulux.Api.DTOs;
using Menulux.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Menulux.Api.Controllers;

[Authorize(Roles = "RestaurantAdmin,Waiter,Cashier")]
[ApiController]
[Route("api/products")]
public class ProductsController : RestaurantScopedController
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db)
    {
        _db = db;
    }

    private static ProductDto ToDto(Product p) =>
        new(p.Id, p.CategoryId, p.Name, p.Description, p.Price, p.ImageUrl, p.IsAvailable, p.SortOrder);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll([FromQuery] Guid? categoryId)
    {
        var restaurantId = GetRestaurantId();
        if (restaurantId is null) return Forbid();

        var query = _db.Products.Where(p => p.RestaurantId == restaurantId);
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        var products = await query.OrderBy(p => p.SortOrder).ToListAsync();
        return Ok(products.Select(ToDto));
    }

    [Authorize(Roles = "RestaurantAdmin")]
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest request)
    {
        var restaurantId = GetRestaurantId();
        if (restaurantId is null) return Forbid();

        var categoryBelongsToRestaurant = await _db.Categories
            .AnyAsync(c => c.Id == request.CategoryId && c.RestaurantId == restaurantId);
        if (!categoryBelongsToRestaurant) return BadRequest(new { message = "Geçersiz kategori." });

        var product = new Product
        {
            RestaurantId = restaurantId.Value,
            CategoryId = request.CategoryId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            ImageUrl = request.ImageUrl,
            IsAvailable = request.IsAvailable,
            SortOrder = request.SortOrder,
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return Ok(ToDto(product));
    }

    [Authorize(Roles = "RestaurantAdmin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProductRequest request)
    {
        var restaurantId = GetRestaurantId();
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.RestaurantId == restaurantId);
        if (product is null) return NotFound();

        var categoryBelongsToRestaurant = await _db.Categories
            .AnyAsync(c => c.Id == request.CategoryId && c.RestaurantId == restaurantId);
        if (!categoryBelongsToRestaurant) return BadRequest(new { message = "Geçersiz kategori." });

        product.CategoryId = request.CategoryId;
        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.ImageUrl = request.ImageUrl;
        product.IsAvailable = request.IsAvailable;
        product.SortOrder = request.SortOrder;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "RestaurantAdmin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var restaurantId = GetRestaurantId();
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.RestaurantId == restaurantId);
        if (product is null) return NotFound();

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
