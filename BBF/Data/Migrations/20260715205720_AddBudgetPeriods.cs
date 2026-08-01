using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBF.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentCategoryId",
                table: "BudgetCategories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RolloverBehavior",
                table: "BudgetCategories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Reset");

            migrationBuilder.CreateTable(
                name: "BudgetPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetPeriods_UserGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BudgetLineItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BudgetPeriodId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    BudgetedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RolledOverAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetLineItems_BudgetCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "BudgetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetLineItems_BudgetPeriods_BudgetPeriodId",
                        column: x => x.BudgetPeriodId,
                        principalTable: "BudgetPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCategories_ParentCategoryId",
                table: "BudgetCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLineItems_BudgetPeriodId_CategoryId",
                table: "BudgetLineItems",
                columns: new[] { "BudgetPeriodId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLineItems_CategoryId",
                table: "BudgetLineItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_GroupId_StartDate",
                table: "BudgetPeriods",
                columns: new[] { "GroupId", "StartDate" },
                unique: true,
                filter: "[GroupId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetCategories_BudgetCategories_ParentCategoryId",
                table: "BudgetCategories",
                column: "ParentCategoryId",
                principalTable: "BudgetCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetCategories_BudgetCategories_ParentCategoryId",
                table: "BudgetCategories");

            migrationBuilder.DropTable(
                name: "BudgetLineItems");

            migrationBuilder.DropTable(
                name: "BudgetPeriods");

            migrationBuilder.DropIndex(
                name: "IX_BudgetCategories_ParentCategoryId",
                table: "BudgetCategories");

            migrationBuilder.DropColumn(
                name: "ParentCategoryId",
                table: "BudgetCategories");

            migrationBuilder.DropColumn(
                name: "RolloverBehavior",
                table: "BudgetCategories");
        }
    }
}
