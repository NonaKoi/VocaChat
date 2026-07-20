using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAiReplyTimingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FixedReplyDelayMilliseconds",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1200L);

            migrationBuilder.AddColumn<long>(
                name: "MaximumReplyDelayMilliseconds",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1800L);

            migrationBuilder.AddColumn<long>(
                name: "MinimumReplyDelayMilliseconds",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 800L);

            migrationBuilder.AddColumn<string>(
                name: "ReplyDelayMode",
                table: "AutonomousInteractionSettings",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "RandomRange");

            migrationBuilder.AddColumn<long>(
                name: "FixedReplyDelayMilliseconds",
                table: "AiAccountAutonomySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1200L);

            migrationBuilder.AddColumn<long>(
                name: "MaximumReplyDelayMilliseconds",
                table: "AiAccountAutonomySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1800L);

            migrationBuilder.AddColumn<long>(
                name: "MinimumReplyDelayMilliseconds",
                table: "AiAccountAutonomySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 800L);

            migrationBuilder.AddColumn<string>(
                name: "ReplyDelayMode",
                table: "AiAccountAutonomySettings",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "RandomRange");

            migrationBuilder.AddColumn<bool>(
                name: "UseGlobalReplyDelay",
                table: "AiAccountAutonomySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousInteractionSettings_ReplyDelayMode",
                table: "AutonomousInteractionSettings",
                sql: "\"ReplyDelayMode\" IN ('Fixed', 'RandomRange')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousInteractionSettings_ReplyDelayValues",
                table: "AutonomousInteractionSettings",
                sql: "\"FixedReplyDelayMilliseconds\" >= 0 AND \"MinimumReplyDelayMilliseconds\" >= 0 AND \"MaximumReplyDelayMilliseconds\" >= 0 AND \"MinimumReplyDelayMilliseconds\" <= \"MaximumReplyDelayMilliseconds\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiAccountAutonomySettings_ReplyDelayMode",
                table: "AiAccountAutonomySettings",
                sql: "\"ReplyDelayMode\" IN ('Fixed', 'RandomRange')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiAccountAutonomySettings_ReplyDelayValues",
                table: "AiAccountAutonomySettings",
                sql: "\"FixedReplyDelayMilliseconds\" >= 0 AND \"MinimumReplyDelayMilliseconds\" >= 0 AND \"MaximumReplyDelayMilliseconds\" >= 0 AND \"MinimumReplyDelayMilliseconds\" <= \"MaximumReplyDelayMilliseconds\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousInteractionSettings_ReplyDelayMode",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousInteractionSettings_ReplyDelayValues",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiAccountAutonomySettings_ReplyDelayMode",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiAccountAutonomySettings_ReplyDelayValues",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropColumn(
                name: "FixedReplyDelayMilliseconds",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "MaximumReplyDelayMilliseconds",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "MinimumReplyDelayMilliseconds",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "ReplyDelayMode",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "FixedReplyDelayMilliseconds",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropColumn(
                name: "MaximumReplyDelayMilliseconds",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropColumn(
                name: "MinimumReplyDelayMilliseconds",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropColumn(
                name: "ReplyDelayMode",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropColumn(
                name: "UseGlobalReplyDelay",
                table: "AiAccountAutonomySettings");
        }
    }
}
