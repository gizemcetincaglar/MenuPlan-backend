namespace Menulux.Api.Models;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }

    public string ReferenceNo { get; set; } = string.Empty;
    public int OrderNumber { get; set; }
    public string? CustomerName { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public OrderType Type { get; set; } = OrderType.DineIn;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public static string BuildReferenceNo(Guid id) =>
        $"ORD-{id.ToString("N")[..8].ToUpperInvariant()}";

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
