namespace KitchenAssistant.Data;

// A product on one user's private list. Every query must filter by UserId.
public class Product
{
    public int Id { get; set; }

    public required string UserId { get; set; }

    public required string Name { get; set; }

    // Trimmed Name through ToUpperInvariant(); backs the per-user duplicate rule.
    public required string NormalizedName { get; set; }

    public ProductCategory Category { get; set; }

    public string? Quantity { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public StorageLocation? StorageLocation { get; set; }
}
