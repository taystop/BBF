using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBF.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "PlaidConnections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ChatConversations",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "BudgetCategories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatConversationShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<int>(type: "int", nullable: false),
                    SharedWithGroupId = table.Column<int>(type: "int", nullable: true),
                    SharedWithUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    SharedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatConversationShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatConversationShares_AspNetUsers_SharedWithUserId",
                        column: x => x.SharedWithUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatConversationShares_ChatConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatConversationShares_UserGroups_SharedWithGroupId",
                        column: x => x.SharedWithGroupId,
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserGroupMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserGroupMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserGroupMembers_UserGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_GroupId",
                table: "Transactions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaidConnections_GroupId",
                table: "PlaidConnections",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_UserId",
                table: "ChatConversations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCategories_GroupId",
                table: "BudgetCategories",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversationShares_ConversationId",
                table: "ChatConversationShares",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversationShares_SharedWithGroupId",
                table: "ChatConversationShares",
                column: "SharedWithGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversationShares_SharedWithUserId",
                table: "ChatConversationShares",
                column: "SharedWithUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMembers_GroupId_UserId",
                table: "UserGroupMembers",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMembers_UserId",
                table: "UserGroupMembers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetCategories_UserGroups_GroupId",
                table: "BudgetCategories",
                column: "GroupId",
                principalTable: "UserGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatConversations_AspNetUsers_UserId",
                table: "ChatConversations",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaidConnections_UserGroups_GroupId",
                table: "PlaidConnections",
                column: "GroupId",
                principalTable: "UserGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_UserGroups_GroupId",
                table: "Transactions",
                column: "GroupId",
                principalTable: "UserGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetCategories_UserGroups_GroupId",
                table: "BudgetCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatConversations_AspNetUsers_UserId",
                table: "ChatConversations");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaidConnections_UserGroups_GroupId",
                table: "PlaidConnections");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_UserGroups_GroupId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "ChatConversationShares");

            migrationBuilder.DropTable(
                name: "UserGroupMembers");

            migrationBuilder.DropTable(
                name: "UserGroups");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_GroupId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_PlaidConnections_GroupId",
                table: "PlaidConnections");

            migrationBuilder.DropIndex(
                name: "IX_ChatConversations_UserId",
                table: "ChatConversations");

            migrationBuilder.DropIndex(
                name: "IX_BudgetCategories_GroupId",
                table: "BudgetCategories");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "PlaidConnections");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "BudgetCategories");
        }
    }
}
