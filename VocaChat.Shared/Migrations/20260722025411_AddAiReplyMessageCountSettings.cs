using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAiReplyMessageCountSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaximumReplyMessageCount",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<int>(
                name: "MinimumReplyMessageCount",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "MaximumReplyMessageCount",
                table: "AiAccountAutonomySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<int>(
                name: "MinimumReplyMessageCount",
                table: "AiAccountAutonomySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "UseGlobalReplyMessageCount",
                table: "AiAccountAutonomySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousInteractionSettings_ReplyMessageCountRange",
                table: "AutonomousInteractionSettings",
                sql: "\"MinimumReplyMessageCount\" BETWEEN 1 AND 4 AND \"MaximumReplyMessageCount\" BETWEEN 1 AND 4 AND \"MinimumReplyMessageCount\" <= \"MaximumReplyMessageCount\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiAccountAutonomySettings_ReplyMessageCountRange",
                table: "AiAccountAutonomySettings",
                sql: "\"MinimumReplyMessageCount\" BETWEEN 1 AND 4 AND \"MaximumReplyMessageCount\" BETWEEN 1 AND 4 AND \"MinimumReplyMessageCount\" <= \"MaximumReplyMessageCount\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousInteractionSettings_ReplyMessageCountRange",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiAccountAutonomySettings_ReplyMessageCountRange",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropColumn(
                name: "MaximumReplyMessageCount",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "MinimumReplyMessageCount",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "MaximumReplyMessageCount",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropColumn(
                name: "MinimumReplyMessageCount",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropColumn(
                name: "UseGlobalReplyMessageCount",
                table: "AiAccountAutonomySettings");
        }
    }
}
