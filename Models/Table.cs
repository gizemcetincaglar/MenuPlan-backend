namespace Menulux.Api.Models;

public class Table
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public int Capacity { get; set; } = 4;
    public bool IsReserved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
