using KitchenAssistant.Data;

namespace KitchenAssistant.Products;

// Polish display text for the product enums.
public static class ProductLabels
{
    public static string For(ProductCategory category) => category switch
    {
        ProductCategory.UseFirst => "Zużyj w pierwszej kolejności",
        ProductCategory.Stored => "W szafkach i zamrażalniku",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    public static string For(StorageLocation location) => location switch
    {
        StorageLocation.Fridge => "Lodówka",
        StorageLocation.Pantry => "Spiżarnia",
        StorageLocation.Freezer => "Zamrażarka",
        _ => throw new ArgumentOutOfRangeException(nameof(location), location, null),
    };
}
