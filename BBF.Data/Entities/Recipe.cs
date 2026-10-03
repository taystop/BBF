namespace BBF.Data.Entities;

public class Recipe
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    public string Name { get; set; } = string.Empty;
    public int BaseServings { get; set; } = 1;
    public string? Instructions { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RecipeIngredient> Ingredients { get; set; } = [];
}
