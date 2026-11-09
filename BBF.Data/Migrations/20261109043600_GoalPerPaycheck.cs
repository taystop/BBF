using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBF.Data.Migrations
{
    /// <inheritdoc />
    public partial class GoalPerPaycheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PerPaycheckAmount",
                table: "Goals",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PerPaycheckAmount",
                table: "Goals");
        }
    }
}
