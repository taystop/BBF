namespace BBF.Data.Entities;

public class BudgetCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyLimit { get; set; }
    public string Color { get; set; } = "#42a5f5";
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public int? GroupId { get; set; }
    public UserGroup? Group { get; set; }

    public List<Transaction> Transactions { get; set; } = [];
}
