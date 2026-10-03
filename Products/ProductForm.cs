using System.ComponentModel.DataAnnotations;
using KitchenAssistant.Data;

namespace KitchenAssistant.Products;

// Input model for the add-product form. Messages are complete Polish sentences (no {0}), so property names never reach the user.
public class ProductForm
{
    [Required(ErrorMessage = "Podaj nazwę produktu.")]
    [StringLength(100, ErrorMessage = "Nazwa produktu może mieć najwyżej 100 znaków.")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Wybierz kategorię.")]
    public ProductCategory? Category { get; set; }

    [StringLength(50, ErrorMessage = "Ilość może mieć najwyżej 50 znaków.")]
    public string? Quantity { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public StorageLocation? StorageLocation { get; set; }
}
