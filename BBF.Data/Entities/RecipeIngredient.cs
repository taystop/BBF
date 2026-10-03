namespace BBF.Data.Entities;

public class RecipeIngredient
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "Each"; // "Teaspoon","Tablespoon","Cup","Pint","Quart","Gallon","FluidOunce","Ounce","Pound","Gram","Kilogram","Each"
    public string? Notes { get; set; } // "diced", "room temperature"
}
