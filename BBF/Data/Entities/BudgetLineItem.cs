namespace BBF.Data.Entities;

public class BudgetLineItem
{
    public int Id { get; set; }
    public int BudgetPeriodId { get; set; }
    public BudgetPeriod BudgetPeriod { get; set; } = null!;

    public int CategoryId { get; set; }
    public BudgetCategory Category { get; set; } = null!;

    public decimal BudgetedAmount { get; set; }
    public decimal RolledOverAmount { get; set; } // computed at period close; 0 for Reset categories
}
