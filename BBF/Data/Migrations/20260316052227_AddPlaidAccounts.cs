using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBF.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaidAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaidAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlaidConnectionId = table.Column<int>(type: "int", nullable: false),
                    PlaidAccountId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OfficialName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Mask = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subtype = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaidAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaidAccounts_PlaidConnections_PlaidConnectionId",
                        column: x => x.PlaidConnectionId,
                        principalTable: "PlaidConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaidAccounts_PlaidAccountId",
                table: "PlaidAccounts",
                column: "PlaidAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaidAccounts_PlaidConnectionId",
                table: "PlaidAccounts",
                column: "PlaidConnectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaidAccounts");
        }
    }
}
