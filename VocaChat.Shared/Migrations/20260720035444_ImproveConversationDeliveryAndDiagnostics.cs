using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class ImproveConversationDeliveryAndDiagnostics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PrivateMessages_PrivateChatId_SentAt_Id",
                table: "PrivateMessages");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_GroupChatId_SentAt_Id",
                table: "GroupMessages");

            migrationBuilder.AddColumn<long>(
                name: "SequenceNumber",
                table: "PrivateMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SequenceNumber",
                table: "GroupMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
                WITH RankedMessages AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "PrivateChatId"
                               ORDER BY "SentAt", "Id") AS "SequenceNumber"
                    FROM "PrivateMessages"
                )
                UPDATE "PrivateMessages"
                SET "SequenceNumber" = (
                    SELECT "SequenceNumber"
                    FROM RankedMessages
                    WHERE RankedMessages."Id" = "PrivateMessages"."Id"
                );
                """);

            migrationBuilder.Sql(
                """
                WITH RankedMessages AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "GroupChatId"
                               ORDER BY "SentAt", "Id") AS "SequenceNumber"
                    FROM "GroupMessages"
                )
                UPDATE "GroupMessages"
                SET "SequenceNumber" = (
                    SELECT "SequenceNumber"
                    FROM RankedMessages
                    WHERE RankedMessages."Id" = "GroupMessages"."Id"
                );
                """);

            migrationBuilder.AddColumn<string>(
                name: "ConsecutiveMessageDelayMode",
                table: "AutonomousInteractionSettings",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "RandomRange");

            migrationBuilder.AddColumn<long>(
                name: "FixedConsecutiveMessageDelayMilliseconds",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 700L);

            migrationBuilder.AddColumn<long>(
                name: "MaximumConsecutiveMessageDelayMilliseconds",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1200L);

            migrationBuilder.AddColumn<int>(
                name: "MaximumConsecutiveQuestionTurns",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<long>(
                name: "MinimumConsecutiveMessageDelayMilliseconds",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 400L);

            migrationBuilder.AddColumn<string>(
                name: "ConsecutiveMessageDelayMode",
                table: "AiAccountAutonomySettings",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "RandomRange");

            migrationBuilder.AddColumn<long>(
                name: "FixedConsecutiveMessageDelayMilliseconds",
                table: "AiAccountAutonomySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 700L);

            migrationBuilder.AddColumn<long>(
                name: "MaximumConsecutiveMessageDelayMilliseconds",
                table: "AiAccountAutonomySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1200L);

            migrationBuilder.AddColumn<int>(
                name: "MaximumConsecutiveQuestionTurns",
                table: "AiAccountAutonomySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<long>(
                name: "MinimumConsecutiveMessageDelayMilliseconds",
                table: "AiAccountAutonomySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 400L);

            migrationBuilder.AddColumn<bool>(
                name: "UseGlobalConsecutiveMessageDelay",
                table: "AiAccountAutonomySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseGlobalQuestionPolicy",
                table: "AiAccountAutonomySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "AiInteractionDiagnosticLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Scenario = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AiAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    WasRecovered = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInteractionDiagnosticLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrivateMessages_PrivateChatId_SentAt",
                table: "PrivateMessages",
                columns: new[] { "PrivateChatId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivateMessages_PrivateChatId_SequenceNumber",
                table: "PrivateMessages",
                columns: new[] { "PrivateChatId", "SequenceNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PrivateMessages_SequenceNumber_Positive",
                table: "PrivateMessages",
                sql: "\"SequenceNumber\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_GroupChatId_SentAt",
                table: "GroupMessages",
                columns: new[] { "GroupChatId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_GroupChatId_SequenceNumber",
                table: "GroupMessages",
                columns: new[] { "GroupChatId", "SequenceNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_GroupMessages_SequenceNumber_Positive",
                table: "GroupMessages",
                sql: "\"SequenceNumber\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousInteractionSettings_ConsecutiveMessageDelayMode",
                table: "AutonomousInteractionSettings",
                sql: "\"ConsecutiveMessageDelayMode\" IN ('Fixed', 'RandomRange')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousInteractionSettings_ConsecutiveMessageDelayValues",
                table: "AutonomousInteractionSettings",
                sql: "\"FixedConsecutiveMessageDelayMilliseconds\" >= 0 AND \"MinimumConsecutiveMessageDelayMilliseconds\" >= 0 AND \"MaximumConsecutiveMessageDelayMilliseconds\" >= 0 AND \"MinimumConsecutiveMessageDelayMilliseconds\" <= \"MaximumConsecutiveMessageDelayMilliseconds\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousInteractionSettings_MaximumConsecutiveQuestionTurns",
                table: "AutonomousInteractionSettings",
                sql: "\"MaximumConsecutiveQuestionTurns\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiAccountAutonomySettings_ConsecutiveMessageDelayMode",
                table: "AiAccountAutonomySettings",
                sql: "\"ConsecutiveMessageDelayMode\" IN ('Fixed', 'RandomRange')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiAccountAutonomySettings_ConsecutiveMessageDelayValues",
                table: "AiAccountAutonomySettings",
                sql: "\"FixedConsecutiveMessageDelayMilliseconds\" >= 0 AND \"MinimumConsecutiveMessageDelayMilliseconds\" >= 0 AND \"MaximumConsecutiveMessageDelayMilliseconds\" >= 0 AND \"MinimumConsecutiveMessageDelayMilliseconds\" <= \"MaximumConsecutiveMessageDelayMilliseconds\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiAccountAutonomySettings_MaximumConsecutiveQuestionTurns",
                table: "AiAccountAutonomySettings",
                sql: "\"MaximumConsecutiveQuestionTurns\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_AiInteractionDiagnosticLogs_AiAccountId",
                table: "AiInteractionDiagnosticLogs",
                column: "AiAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AiInteractionDiagnosticLogs_OccurredAt",
                table: "AiInteractionDiagnosticLogs",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiInteractionDiagnosticLogs");

            migrationBuilder.DropIndex(
                name: "IX_PrivateMessages_PrivateChatId_SentAt",
                table: "PrivateMessages");

            migrationBuilder.DropIndex(
                name: "IX_PrivateMessages_PrivateChatId_SequenceNumber",
                table: "PrivateMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PrivateMessages_SequenceNumber_Positive",
                table: "PrivateMessages");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_GroupChatId_SentAt",
                table: "GroupMessages");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_GroupChatId_SequenceNumber",
                table: "GroupMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GroupMessages_SequenceNumber_Positive",
                table: "GroupMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousInteractionSettings_ConsecutiveMessageDelayMode",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousInteractionSettings_ConsecutiveMessageDelayValues",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousInteractionSettings_MaximumConsecutiveQuestionTurns",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiAccountAutonomySettings_ConsecutiveMessageDelayMode",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiAccountAutonomySettings_ConsecutiveMessageDelayValues",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiAccountAutonomySettings_MaximumConsecutiveQuestionTurns",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                table: "PrivateMessages");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "ConsecutiveMessageDelayMode",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "FixedConsecutiveMessageDelayMilliseconds",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "MaximumConsecutiveMessageDelayMilliseconds",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "MaximumConsecutiveQuestionTurns",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "MinimumConsecutiveMessageDelayMilliseconds",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "ConsecutiveMessageDelayMode",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropColumn(
                name: "FixedConsecutiveMessageDelayMilliseconds",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropColumn(
                name: "MaximumConsecutiveMessageDelayMilliseconds",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropColumn(
                name: "MaximumConsecutiveQuestionTurns",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropColumn(
                name: "MinimumConsecutiveMessageDelayMilliseconds",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropColumn(
                name: "UseGlobalConsecutiveMessageDelay",
                table: "AiAccountAutonomySettings");

            migrationBuilder.DropColumn(
                name: "UseGlobalQuestionPolicy",
                table: "AiAccountAutonomySettings");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateMessages_PrivateChatId_SentAt_Id",
                table: "PrivateMessages",
                columns: new[] { "PrivateChatId", "SentAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_GroupChatId_SentAt_Id",
                table: "GroupMessages",
                columns: new[] { "GroupChatId", "SentAt", "Id" });
        }
    }
}
