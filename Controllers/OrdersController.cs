using Menulux.Api.Data;
using Menulux.Api.DTOs;
using Menulux.Api.Hubs;
using Menulux.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Menulux.Api.Controllers;

[Authorize(Roles = "RestaurantAdmin,Kitchen,Waiter,Cashier")]
[ApiController]
[Route("api/orders")]
public class OrdersController : RestaurantScopedController
{
    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;

    public OrdersController(AppDbContext db, IHubContext<OrderHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private static OrderDto ToDto(Order order)
    {
        var total = order.Items.Sum(i => i.UnitPrice * i.Quantity);
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
            order.Items.Select(i => new OrderItemDto(i.Id, i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.Notes)),
            total,
            paid,
            total - paid,
            order.Payments.OrderBy(p => p.CreatedAt).Select(p => new PaymentDto(p.Id, p.Amount, p.Method, p.CreatedAt)));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAll(
        [FromQuery] bool activeOnly = true,
        [FromQuery] OrderType? type = null,
        [FromQuery] OrderStatus? status = null,
        [FromQuery] int? orderNumber = null,
        [FromQuery] string? search = null)
    {
        var restaurantId = GetRestaurantId();
        if (restaurantId is null) return Forbid();

        var query = _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Where(o => o.RestaurantId == restaurantId);

        if (status is not null)
        {
            query = query.Where(o => o.Status == status);
        }
        else if (activeOnly)
        {
            query = query.Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);
        }

        if (type is not null)
        {
            query = query.Where(o => o.Type == type);
        }

        if (orderNumber is not null)
        {
            query = query.Where(o => o.OrderNumber == orderNumber);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(o =>
                o.ReferenceNo.ToLower().Contains(q) ||
                o.TableNumber.ToLower().Contains(q) ||
                (o.CustomerName != null && o.CustomerName.ToLower().Contains(q)) ||
                o.OrderNumber.ToString() == q);
        }

        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
        return Ok(orders.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id)
    {
        var restaurantId = GetRestaurantId();
        if (restaurantId is null) return Forbid();

        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id && o.RestaurantId == restaurantId);

        if (order is null) return NotFound();

        return Ok(ToDto(order));
    }

    [Authorize(Roles = "RestaurantAdmin,Waiter,Cashier")]
    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new { message = "Sepet boş olamaz." });
        }

        var restaurantId = GetRestaurantId();
        if (restaurantId is null) return Forbid();

        var tableNumber = request.TableNumber.Trim();

        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Where(p => p.RestaurantId == restaurantId && productIds.Contains(p.Id) && p.IsAvailable)
            .ToListAsync();

        if (products.Count != productIds.Distinct().Count())
        {
            return BadRequest(new { message = "Seçilen ürünlerden bazıları artık mevcut değil." });
        }

        var existingOrder = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Where(o => o.RestaurantId == restaurantId
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
                .Where(o => o.RestaurantId == restaurantId && o.CreatedAt >= todayUtc)
                .CountAsync() + 1;

            order = new Order
            {
                ReferenceNo = string.Empty,
                RestaurantId = restaurantId.Value,
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

        var table = await _db.Tables.FirstOrDefaultAsync(t => t.RestaurantId == restaurantId && t.Name == tableNumber && t.IsReserved);
        if (table is not null)
        {
            table.IsReserved = false;
        }

        await _db.SaveChangesAsync();

        var dto = ToDto(order);
        await _hub.Clients.Group(restaurantId.ToString()!).SendAsync(existingOrder is null ? "OrderCreated" : "OrderUpdated", dto);

        return Ok(dto);
    }

    [Authorize(Roles = "RestaurantAdmin,Cashier")]
    [HttpGet("table/{tableNumber}")]
    public async Task<ActionResult<TableBillDto>> GetTableBill(string tableNumber)
    {
        var restaurantId = GetRestaurantId();
        if (restaurantId is null) return Forbid();

        tableNumber = tableNumber.Trim();

        var orders = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Where(o => o.RestaurantId == restaurantId
                && o.TableNumber == tableNumber
                && o.Status != OrderStatus.Completed
                && o.Status != OrderStatus.Cancelled)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        if (orders.Count == 0) return NotFound();

        var dtos = orders.Select(ToDto).ToList();
        var total = dtos.Sum(o => o.Total);
        var paid = dtos.Sum(o => o.AmountPaid);

        return Ok(new TableBillDto(tableNumber, dtos, total, paid, total - paid));
    }

    [Authorize(Roles = "RestaurantAdmin,Kitchen")]
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(Guid id, UpdateOrderStatusRequest request)
    {
        var restaurantId = GetRestaurantId();
        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id && o.RestaurantId == restaurantId);

        if (order is null) return NotFound();

        var isKitchenOnly = !User.IsInRole("RestaurantAdmin");
        if (isKitchenOnly && request.Status is OrderStatus.Completed or OrderStatus.Cancelled)
        {
            return Forbid();
        }

        order.Status = request.Status;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var dto = ToDto(order);
        await _hub.Clients.Group(restaurantId.ToString()!).SendAsync("OrderUpdated", dto);

        return Ok(dto);
    }

    [Authorize(Roles = "RestaurantAdmin,Cashier")]
    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<OrderDto>> AddPayment(Guid id, CreatePaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Ödeme tutarı sıfırdan büyük olmalı." });
        }

        var restaurantId = GetRestaurantId();
        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id && o.RestaurantId == restaurantId);

        if (order is null) return NotFound();

        var total = order.Items.Sum(i => i.UnitPrice * i.Quantity);
        var alreadyPaid = order.Payments.Sum(p => p.Amount);
        var remaining = total - alreadyPaid;

        if (request.Amount > remaining)
        {
            return BadRequest(new { message = $"Ödeme tutarı kalan bakiyeyi ({remaining:0.00}) aşamaz." });
        }

        var payment = new Payment
        {
            OrderId = order.Id,
            Amount = request.Amount,
            Method = request.Method,
        };
        _db.Payments.Add(payment);

        var newPaid = alreadyPaid + request.Amount;
        order.PaymentStatus = newPaid >= total ? PaymentStatus.Paid : PaymentStatus.PartiallyPaid;
        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            order.Status = OrderStatus.Completed;
        }
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var dto = ToDto(order);
        await _hub.Clients.Group(restaurantId.ToString()!).SendAsync("OrderUpdated", dto);

        return Ok(dto);
    }

    [Authorize(Roles = "RestaurantAdmin,Cashier")]
    [HttpPost("table/{tableNumber}/payments")]
    public async Task<ActionResult<TableBillDto>> AddTablePayment(string tableNumber, CreatePaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Ödeme tutarı sıfırdan büyük olmalı." });
        }

        var restaurantId = GetRestaurantId();
        if (restaurantId is null) return Forbid();

        tableNumber = tableNumber.Trim();

        var orders = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Where(o => o.RestaurantId == restaurantId
                && o.TableNumber == tableNumber
                && o.Status != OrderStatus.Completed
                && o.Status != OrderStatus.Cancelled)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        if (orders.Count == 0) return NotFound();

        var totalRemaining = orders.Sum(o => o.Items.Sum(i => i.UnitPrice * i.Quantity) - o.Payments.Sum(p => p.Amount));
        if (request.Amount > totalRemaining)
        {
            return BadRequest(new { message = $"Ödeme tutarı kalan bakiyeyi ({totalRemaining:0.00}) aşamaz." });
        }

        var amountLeft = request.Amount;
        foreach (var order in orders)
        {
            if (amountLeft <= 0) break;

            var orderTotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);
            var orderPaid = order.Payments.Sum(p => p.Amount);
            var orderRemaining = orderTotal - orderPaid;
            if (orderRemaining <= 0) continue;

            var portion = Math.Min(orderRemaining, amountLeft);
            var payment = new Payment { OrderId = order.Id, Amount = portion, Method = request.Method };
            _db.Payments.Add(payment);
            amountLeft -= portion;

            var newPaid = orderPaid + portion;
            order.PaymentStatus = newPaid >= orderTotal ? PaymentStatus.Paid : PaymentStatus.PartiallyPaid;
            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                order.Status = OrderStatus.Completed;
            }
            order.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        foreach (var order in orders)
        {
            await _hub.Clients.Group(restaurantId.ToString()!).SendAsync("OrderUpdated", ToDto(order));
        }

        var dtos = orders.Select(ToDto).ToList();
        var total = dtos.Sum(o => o.Total);
        var paid = dtos.Sum(o => o.AmountPaid);

        return Ok(new TableBillDto(tableNumber, dtos, total, paid, total - paid));
    }

    [Authorize(Roles = "RestaurantAdmin,Cashier")]
    [HttpGet("payments")]
    public async Task<ActionResult<IEnumerable<PaymentHistoryItemDto>>> GetPaymentHistory(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] PaymentMethod? method,
        [FromQuery] string? tableNumber)
    {
        var restaurantId = GetRestaurantId();
        if (restaurantId is null) return Forbid();

        var fromUtc = DateTime.SpecifyKind((from ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind((to ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        var query = _db.Payments
            .Include(p => p.Order)
            .Where(p => p.Order!.RestaurantId == restaurantId && p.CreatedAt >= fromUtc && p.CreatedAt <= toUtc);

        if (method is not null)
        {
            query = query.Where(p => p.Method == method);
        }

        if (!string.IsNullOrWhiteSpace(tableNumber))
        {
            var trimmed = tableNumber.Trim();
            query = query.Where(p => p.Order!.TableNumber == trimmed);
        }

        var payments = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

        return Ok(payments.Select(p => new PaymentHistoryItemDto(
            p.Id, p.OrderId, p.Order!.ReferenceNo, p.Order!.TableNumber, p.Amount, p.Method, p.CreatedAt)));
    }

    [Authorize(Roles = "RestaurantAdmin,Cashier")]
    [HttpGet("payments/report")]
    public async Task<ActionResult<PaymentReportDto>> GetPaymentReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        var restaurantId = GetRestaurantId();
        if (restaurantId is null) return Forbid();

        var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromUtc = DateTime.SpecifyKind(fromDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(toDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        var payments = await _db.Payments
            .Include(p => p.Order)
            .Where(p => p.Order!.RestaurantId == restaurantId && p.CreatedAt >= fromUtc && p.CreatedAt <= toUtc)
            .ToListAsync();

        var byMethod = payments
            .GroupBy(p => p.Method)
            .Select(g => new PaymentMethodBreakdownDto(g.Key, g.Sum(p => p.Amount), g.Count()))
            .OrderBy(m => m.Method);

        var byDay = payments
            .GroupBy(p => DateOnly.FromDateTime(p.CreatedAt))
            .Select(g => new DailyRevenueDto(g.Key, g.Sum(p => p.Amount), g.Count()))
            .OrderBy(d => d.Date);

        return Ok(new PaymentReportDto(
            fromDate,
            toDate,
            payments.Sum(p => p.Amount),
            payments.Count,
            byMethod,
            byDay));
    }
}
