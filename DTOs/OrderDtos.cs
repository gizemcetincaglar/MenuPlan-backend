using Menulux.Api.Models;

namespace Menulux.Api.DTOs;

public record OrderItemDto(Guid Id, Guid? ProductId, string ProductName, decimal UnitPrice, int Quantity, string? Notes);

public record PaymentDto(Guid Id, decimal Amount, PaymentMethod Method, DateTime CreatedAt);

public record OrderDto(
    Guid Id,
    string ReferenceNo,
    int OrderNumber,
    string? CustomerName,
    string TableNumber,
    OrderType Type,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IEnumerable<OrderItemDto> Items,
    decimal Total,
    decimal AmountPaid,
    decimal RemainingAmount,
    IEnumerable<PaymentDto> Payments);

public record CreateOrderItemRequest(Guid ProductId, int Quantity, string? Notes);

public record CreateOrderRequest(string TableNumber, OrderType? Type, string? Notes, List<CreateOrderItemRequest> Items, string? CustomerName = null);

public record UpdateOrderStatusRequest(OrderStatus Status);

public record CreatePaymentRequest(decimal Amount, PaymentMethod Method);

public record TableBillDto(
    string TableNumber,
    IEnumerable<OrderDto> Orders,
    decimal Total,
    decimal AmountPaid,
    decimal RemainingAmount);

public record PaymentHistoryItemDto(
    Guid PaymentId,
    Guid OrderId,
    string ReferenceNo,
    string TableNumber,
    decimal Amount,
    PaymentMethod Method,
    DateTime CreatedAt);

public record PaymentMethodBreakdownDto(PaymentMethod Method, decimal Total, int Count);

public record DailyRevenueDto(DateOnly Date, decimal Total, int PaymentCount);

public record PaymentReportDto(
    DateOnly From,
    DateOnly To,
    decimal TotalRevenue,
    int PaymentCount,
    IEnumerable<PaymentMethodBreakdownDto> ByMethod,
    IEnumerable<DailyRevenueDto> ByDay);
