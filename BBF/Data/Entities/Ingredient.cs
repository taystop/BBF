namespace BBF.Data.Entities;

public class Ingredient
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    public string Name { get; set; } = string.Empty;
    public string MeasurementType { get; set; } = "Count"; // "Volume", "Weight", "Count"

    public int? SectionId { get; set; }
    public GrocerySection? Section { get; set; }

    // Optional purchasable package size (e.g. "5 lb bag"), used to compute "buy N" on the grocery list.
    // PackageUnit must be the same MeasurementType as this ingredient.
    public decimal? PackageQuantity { get; set; }
    public string? PackageUnit { get; set; }

    public List<RecipeIngredient> RecipeIngredients { get; set; } = [];
}
