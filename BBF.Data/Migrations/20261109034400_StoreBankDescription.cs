using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBF.Data.Migrations
{
    /// <inheritdoc />
    public partial class StoreBankDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankDescription",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);

            // Deposits Plaid tagged as income but that were stored as a store refund (e.g. a Walmart
            // paycheck filed under Groceries). Hand-picked types are left alone.
            migrationBuilder.Sql(
                "UPDATE Transactions SET Kind = 'Income', CategoryId = NULL " +
                "WHERE Amount < 0 AND Kind = 'Spending' AND KindSetManually = 0 AND PlaidCategory LIKE 'INCOME%'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankDescription",
                table: "Transactions");
        }
    }
}
