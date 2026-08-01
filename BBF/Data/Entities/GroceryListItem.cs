namespace BBF.Data.Entities;

public class GroceryListItem
{
    public int Id { get; set; }
    public int GroceryListId { get; set; }
    public GroceryList GroceryList { get; set; } = null!;

    public int? IngredientId { get; set; } // null for freeform manually-added items
    public Ingredient? Ingredient { get; set; }

    // Snapshot of the ingredient's section at generation time, not a live lookup — re-sectioning an
    // ingredient later shouldn't silently rewrite a grocery list that's already being shopped from.
    public int? SectionId { get; set; }
    public GrocerySection? Section { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal? RequiredQuantity { get; set; } // null for freeform items
    public string? RequiredUnit { get; set; }
    public int? PackagesToBuy { get; set; } // snapshot computed from Ingredient.PackageQuantity at generation time

    public bool IsChecked { get; set; }
    public bool IsManuallyAdded { get; set; }
}
