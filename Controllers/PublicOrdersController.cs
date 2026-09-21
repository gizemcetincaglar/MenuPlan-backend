using Menulux.Api.Data;
using Menulux.Api.DTOs;
using Menulux.Api.Hubs;
using Menulux.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Menulux.Api.Controllers;

[ApiController]
[Route("api/public/orders")]
public class PublicOrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;

    public PublicOrdersController(AppDbContext db, IHubContext<OrderHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private static OrderDto ToDto(Order order, IEnumerable<OrderItem> items)
    {
        var itemList = items.ToList();
        var total = itemList.Sum(i => i.UnitPrice * i.Quantity);
        var paid = order.Payments.Sum(p => p.Amount);

        return new OrderDto(
            order.Id,
            order.ReferenceNo,
            order.OrderNumber,
            order.CustomerName,
            order.TableNumber,
            order.Type,
            order.Status,
            order.PaymentStatus,
            order.Notes,
            order.CreatedAt,
            order.UpdatedAt,
            itemList.Select(i => new OrderItemDto(i.Id, i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.Notes)),
            total,
            paid,
            total - paid,
            order.Payments.OrderBy(p => p.CreatedAt).Select(p => new PaymentDto(p.Id, p.Amount, p.Method, p.CreatedAt)));
    }

    [HttpPost("{slug}")]
    public async Task<ActionResult<OrderDto>> Create(string slug, CreateOrderRequest request)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new { message = "Sepet boş olamaz." });
        }

        var restaurant = await _db.Restaurants.FirstOrDefaultAsync(r => r.Slug == slug);
        if (restaurant is null) return NotFound();

        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Where(p => p.RestaurantId == restaurant.Id && productIds.Contains(p.Id) && p.IsAvailable)
            .ToListAsync();

        if (products.Count != productIds.Distinct().Count())
        {
            return BadRequest(new { message = "Sepetteki bazı ürünler artık mevcut değil." });
        }

        var tableNumber = request.TableNumber.Trim();

        var existingOrder = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Where(o => o.RestaurantId == restaurant.Id
                && o.TableNumber == tableNumber
                && o.Status != OrderStatus.Completed
                && o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        var order = existingOrder;
        if (order is null)
        {
            var todayUtc = DateTime.UtcNow.Date;
            var nextOrderNumber = await _db.Orders
                .Where(o => o.RestaurantId == restaurant.Id && o.CreatedAt >= todayUtc)
                .CountAsync() + 1;

            order = new Order
            {
                ReferenceNo = string.Empty,
                RestaurantId = restaurant.Id,
                TableNumber = tableNumber,
                Type = request.Type ?? OrderType.DineIn,
                OrderNumber = nextOrderNumber,
                CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? null : request.CustomerName.Trim(),
            };
        }

        if (existingOrder is null)
        {
            order.ReferenceNo = Order.BuildReferenceNo(order.Id);
        }

        if (request.Notes is not null)
        {
            order.Notes = order.Notes is null ? request.Notes : $"{order.Notes} | {request.Notes}";
        }

        if (existingOrder is null)
        {
            _db.Orders.Add(order);
        }
        else
        {
            if (order.Status == OrderStatus.Ready)
            {
                order.Status = OrderStatus.Pending;
            }
            order.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        var newItems = request.Items.Select(item =>
        {
            var product = products.First(p => p.Id == item.ProductId);
            return new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = item.Quantity,
                Notes = item.Notes,
            };
        }).ToList();

        _db.OrderItems.AddRange(newItems);

        var table = await _db.Tables.FirstOrDefaultAsync(t => t.RestaurantId == restaurant.Id && t.Name == tableNumber && t.IsReserved);
        if (table is not null)
        {
            table.IsReserved = false;
        }

        await _db.SaveChangesAsync();

        var dto = ToDto(order, order.Items);
        await _hub.Clients.Group(restaurant.Id.ToString()).SendAsync(existingOrder is null ? "OrderCreated" : "OrderUpdated", dto);

        return Ok(dto);
    }
}
