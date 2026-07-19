using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAutonomousGroupChatMemberLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutonomousGroupChatMaximumMembers",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousInteractionSettings_GroupChatMaximumMembers",
                table: "AutonomousInteractionSettings",
                sql: "\"AutonomousGroupChatMaximumMembers\" >= 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousInteractionSettings_GroupChatMaximumMembers",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "AutonomousGroupChatMaximumMembers",
                table: "AutonomousInteractionSettings");
        }
    }
}
