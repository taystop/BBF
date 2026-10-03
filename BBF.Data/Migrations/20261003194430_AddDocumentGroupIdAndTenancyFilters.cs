using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBF.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentGroupIdAndTenancyFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Documents",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_GroupId",
                table: "Documents",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_UserGroups_GroupId",
                table: "Documents",
                column: "GroupId",
                principalTable: "UserGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_UserGroups_GroupId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_GroupId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Documents");
        }
    }
}
