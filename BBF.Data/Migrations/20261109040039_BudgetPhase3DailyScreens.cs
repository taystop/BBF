using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBF.Data.Migrations
{
    /// <inheritdoc />
    public partial class BudgetPhase3DailyScreens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LowBalanceThreshold",
                table: "FinanceSettings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MerchantRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    MatchText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MerchantRules_BudgetCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "BudgetCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MerchantRules_UserGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SuggestionDismissals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    Key = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuggestionDismissals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuggestionDismissals_UserGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantRules_CategoryId",
                table: "MerchantRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantRules_GroupId_MatchText",
                table: "MerchantRules",
                columns: new[] { "GroupId", "MatchText" },
                unique: true,
                filter: "[GroupId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SuggestionDismissals_GroupId_Key",
                table: "SuggestionDismissals",
                columns: new[] { "GroupId", "Key" },
                unique: true,
                filter: "[GroupId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MerchantRules");

            migrationBuilder.DropTable(
                name: "SuggestionDismissals");

            migrationBuilder.DropColumn(
                name: "LowBalanceThreshold",
                table: "FinanceSettings");
        }
    }
}
