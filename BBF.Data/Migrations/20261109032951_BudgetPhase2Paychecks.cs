using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBF.Data.Migrations
{
    /// <inheritdoc />
    public partial class BudgetPhase2Paychecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedPayDate",
                table: "BillOccurrences",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivateToUserId",
                table: "Accounts",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaySchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstPayday = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextPayday = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DepositAccountId = table.Column<int>(type: "int", nullable: true),
                    MatchText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPrivate = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaySchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaySchedules_Accounts_DepositAccountId",
                        column: x => x.DepositAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaySchedules_UserGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Paychecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    PayScheduleId = table.Column<int>(type: "int", nullable: false),
                    PayDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionId = table.Column<int>(type: "int", nullable: true),
                    ActualAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paychecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Paychecks_PaySchedules_PayScheduleId",
                        column: x => x.PayScheduleId,
                        principalTable: "PaySchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Paychecks_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Paychecks_GroupId_PayDate",
                table: "Paychecks",
                columns: new[] { "GroupId", "PayDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Paychecks_PayScheduleId_PayDate",
                table: "Paychecks",
                columns: new[] { "PayScheduleId", "PayDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Paychecks_TransactionId",
                table: "Paychecks",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaySchedules_DepositAccountId",
                table: "PaySchedules",
                column: "DepositAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PaySchedules_GroupId",
                table: "PaySchedules",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Paychecks");

            migrationBuilder.DropTable(
                name: "PaySchedules");

            migrationBuilder.DropColumn(
                name: "AssignedPayDate",
                table: "BillOccurrences");

            migrationBuilder.DropColumn(
                name: "PrivateToUserId",
                table: "Accounts");
        }
    }
}
