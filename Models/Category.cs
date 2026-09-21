namespace Menulux.Api.Models;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }

    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
