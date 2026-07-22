using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupConversationDensitySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupChatMaximumMessagesPerTurn",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<int>(
                name: "GroupChatMaximumSpeakersPerTurn",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "GroupChatWholeGroupMaximumSpeakersPerTurn",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousInteractionSettings_GroupChatDensity",
                table: "AutonomousInteractionSettings",
                sql: "\"GroupChatMaximumSpeakersPerTurn\" BETWEEN 1 AND 12 AND \"GroupChatWholeGroupMaximumSpeakersPerTurn\" BETWEEN 1 AND 12 AND \"GroupChatMaximumMessagesPerTurn\" BETWEEN 1 AND 12 AND \"GroupChatMaximumSpeakersPerTurn\" <= \"GroupChatMaximumMessagesPerTurn\" AND \"GroupChatWholeGroupMaximumSpeakersPerTurn\" <= \"GroupChatMaximumMessagesPerTurn\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousInteractionSettings_GroupChatDensity",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "GroupChatMaximumMessagesPerTurn",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "GroupChatMaximumSpeakersPerTurn",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "GroupChatWholeGroupMaximumSpeakersPerTurn",
                table: "AutonomousInteractionSettings");
        }
    }
}
