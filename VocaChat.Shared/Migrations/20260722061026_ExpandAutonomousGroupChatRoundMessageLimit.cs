using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class ExpandAutonomousGroupChatRoundMessageLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousGroupChatRounds_MessageCount",
                table: "AutonomousGroupChatRounds");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousGroupChatRounds_MessageCount",
                table: "AutonomousGroupChatRounds",
                sql: "\"PlannedMessageCount\" BETWEEN 0 AND 12");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousGroupChatRounds_MessageCount",
                table: "AutonomousGroupChatRounds");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousGroupChatRounds_MessageCount",
                table: "AutonomousGroupChatRounds",
                sql: "\"PlannedMessageCount\" BETWEEN 0 AND 9");
        }
    }
}
