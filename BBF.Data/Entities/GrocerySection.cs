namespace BBF.Data.Entities;

public class GrocerySection
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public List<Ingredient> Ingredients { get; set; } = [];
}
