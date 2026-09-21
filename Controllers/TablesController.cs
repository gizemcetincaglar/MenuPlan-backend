using Menulux.Api.Data;
using Menulux.Api.DTOs;
using Menulux.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Menulux.Api.Controllers;

[Authorize(Roles = "RestaurantAdmin,Waiter,Cashier")]
[ApiController]
[Route("api/tables")]
public class TablesController : RestaurantScopedController
{
    private readonly AppDbContext _db;

    public TablesController(AppDbContext db)
    {
        _db = db;
    }

    private static TableDto ToDto(Table table, IEnumerable<Order> activeOrders)
    {
        var orders = activeOrders.Where(o => o.TableNumber == table.Name).ToList();
        var total = orders.SelectMany(o => o.Items).Sum(i => i.UnitPrice * i.Quantity);
        var paid = orders.SelectMany(o => o.Payments).Sum(p => p.Amount);

        var status = orders.Count > 0
            ? TableStatus.Occupied
            : table.IsReserved
                ? TableStatus.Reserved
                : TableStatus.Empty;

        return new TableDto(table.Id, table.Name, table.Zone, table.Capacity, table.IsReserved, status, total, total - paid);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TableDto>>> GetAll()
    {
        var restaurantId = GetRestaurantId();
        if (restaurantId is null) return Forbid();

        var tables = await _db.Tables
            .Where(t => t.RestaurantId == restaurantId)
            .OrderBy(t => t.Name)
            .ToListAsync();

        var activeOrders = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Where(o => o.RestaurantId == restaurantId
                && o.Status != OrderStatus.Completed
                && o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        return Ok(tables.Select(t => ToDto(t, activeOrders)));
    }

    [Authorize(Roles = "RestaurantAdmin")]
    [HttpPost]
    public async Task<ActionResult<TableDto>> Create(CreateTableRequest request)
    {
        var restaurantId = GetRestaurantId();
        if (restaurantId is null) return Forbid();

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Masa adı boş olamaz." });
        }

        var exists = await _db.Tables.AnyAsync(t => t.RestaurantId == restaurantId && t.Name == name);
        if (exists)
        {
            return BadRequest(new { message = "Bu isimde bir masa zaten var." });
        }

        var table = new Table
        {
            RestaurantId = restaurantId.Value,
            Name = name,
            Zone = string.IsNullOrWhiteSpace(request.Zone) ? null : request.Zone.Trim(),
            Capacity = request.Capacity > 0 ? request.Capacity : 4,
        };

        _db.Tables.Add(table);
        await _db.SaveChangesAsync();

        return Ok(ToDto(table, Enumerable.Empty<Order>()));
    }

    [Authorize(Roles = "RestaurantAdmin")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TableDto>> Update(Guid id, UpdateTableRequest request)
    {
        var restaurantId = GetRestaurantId();
        var table = await _db.Tables.FirstOrDefaultAsync(t => t.Id == id && t.RestaurantId == restaurantId);
        if (table is null) return NotFound();

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Masa adı boş olamaz." });
        }

        var duplicate = await _db.Tables.AnyAsync(t => t.RestaurantId == restaurantId && t.Name == name && t.Id != id);
        if (duplicate)
        {
            return BadRequest(new { message = "Bu isimde bir masa zaten var." });
        }

        table.Name = name;
        table.Zone = string.IsNullOrWhiteSpace(request.Zone) ? null : request.Zone.Trim();
        table.Capacity = request.Capacity > 0 ? request.Capacity : 4;
        await _db.SaveChangesAsync();

        var activeOrders = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Where(o => o.RestaurantId == restaurantId
                && o.TableNumber == table.Name
                && o.Status != OrderStatus.Completed
                && o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        return Ok(ToDto(table, activeOrders));
    }

    [Authorize(Roles = "RestaurantAdmin")]
    [HttpPut("{id:guid}/reserve")]
    public async Task<ActionResult<TableDto>> SetReserved(Guid id, SetTableReservedRequest request)
    {
        var restaurantId = GetRestaurantId();
        var table = await _db.Tables.FirstOrDefaultAsync(t => t.Id == id && t.RestaurantId == restaurantId);
        if (table is null) return NotFound();

        var activeOrders = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Where(o => o.RestaurantId == restaurantId
                && o.TableNumber == table.Name
                && o.Status != OrderStatus.Completed
                && o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        if (request.Reserved && activeOrders.Count > 0)
        {
            return BadRequest(new { message = "Masada açık adisyon varken rezerve edilemez." });
        }

        table.IsReserved = request.Reserved;
        await _db.SaveChangesAsync();

        return Ok(ToDto(table, activeOrders));
    }

    [Authorize(Roles = "RestaurantAdmin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var restaurantId = GetRestaurantId();
        var table = await _db.Tables.FirstOrDefaultAsync(t => t.Id == id && t.RestaurantId == restaurantId);
        if (table is null) return NotFound();

        var hasActiveOrder = await _db.Orders.AnyAsync(o => o.RestaurantId == restaurantId
            && o.TableNumber == table.Name
            && o.Status != OrderStatus.Completed
            && o.Status != OrderStatus.Cancelled);

        if (hasActiveOrder)
        {
            return BadRequest(new { message = "Masada açık adisyon varken silinemez." });
        }

        _db.Tables.Remove(table);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
