using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBF.Data.Migrations
{
    /// <inheritdoc />
    public partial class BudgetPhase1Foundations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPending",
                table: "Transactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Transactions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Spending");

            migrationBuilder.AddColumn<bool>(
                name: "KindSetManually",
                table: "Transactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlaidCategory",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoPost",
                table: "RecurringTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MatchText",
                table: "RecurringTransactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookUrl",
                table: "PlaidConnections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AvailableBalance",
                table: "Accounts",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BalanceAsOf",
                table: "Accounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CountPurchasesInBudget",
                table: "Accounts",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Credit card purchases don't count toward the budget by default — the card payment does.
            migrationBuilder.Sql("UPDATE Accounts SET CountPurchasesInBudget = 0 WHERE AccountType = 'CreditCard'");

            // Uncategorized money coming in is income, not a negative expense.
            migrationBuilder.Sql("UPDATE Transactions SET Kind = 'Income' WHERE Amount < 0 AND CategoryId IS NULL");

            migrationBuilder.CreateTable(
                name: "BillOccurrences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    RecurringTransactionId = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionId = table.Column<int>(type: "int", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillOccurrences_RecurringTransactions_RecurringTransactionId",
                        column: x => x.RecurringTransactionId,
                        principalTable: "RecurringTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillOccurrences_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FinanceSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    BudgetStartDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceSettings_UserGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillOccurrences_GroupId_DueDate",
                table: "BillOccurrences",
                columns: new[] { "GroupId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BillOccurrences_RecurringTransactionId_DueDate",
                table: "BillOccurrences",
                columns: new[] { "RecurringTransactionId", "DueDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillOccurrences_TransactionId",
                table: "BillOccurrences",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_GroupId",
                table: "FinanceSettings",
                column: "GroupId",
                unique: true,
                filter: "[GroupId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillOccurrences");

            migrationBuilder.DropTable(
                name: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "IsPending",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "KindSetManually",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PlaidCategory",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AutoPost",
                table: "RecurringTransactions");

            migrationBuilder.DropColumn(
                name: "MatchText",
                table: "RecurringTransactions");

            migrationBuilder.DropColumn(
                name: "WebhookUrl",
                table: "PlaidConnections");

            migrationBuilder.DropColumn(
                name: "AvailableBalance",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BalanceAsOf",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "CountPurchasesInBudget",
                table: "Accounts");
        }
    }
}
