using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAutonomousPrivateChatRounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousPrivateChatSessions_RoundLimits",
                table: "AutonomousPrivateChatSessions");

            migrationBuilder.RenameColumn(
                name: "PlannedMaximumRounds",
                table: "AutonomousPrivateChatSessions",
                newName: "MaximumRounds");

            migrationBuilder.AddColumn<Guid>(
                name: "AutonomousPrivateChatRoundId",
                table: "PrivateMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutonomousSequenceNumber",
                table: "PrivateMessages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContinuationRatePercent",
                table: "AutonomousPrivateChatSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 80);

            migrationBuilder.AddColumn<int>(
                name: "PrivateChatContinuationRatePercent",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 80);

            migrationBuilder.AddColumn<int>(
                name: "PrivateChatMaximumRounds",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.CreateTable(
                name: "AutonomousPrivateChatRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoundNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    IsClosing = table.Column<bool>(type: "INTEGER", nullable: false),
                    OccurrenceProbability = table.Column<double>(type: "REAL", nullable: true),
                    RandomRoll = table.Column<double>(type: "REAL", nullable: true),
                    InitiatorMessageMode = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipientMessageMode = table.Column<int>(type: "INTEGER", nullable: false),
                    InitiatorMessageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipientMessageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomousPrivateChatRounds", x => x.Id);
                    table.CheckConstraint("CK_AutonomousPrivateChatRounds_ClosingProbability", "(\"IsClosing\" = 1 AND \"OccurrenceProbability\" IS NULL AND \"RandomRoll\" IS NULL) OR \"IsClosing\" = 0");
                    table.CheckConstraint("CK_AutonomousPrivateChatRounds_InitiatorSpeaksInNormalRound", "\"IsClosing\" = 1 OR \"InitiatorMessageMode\" <> 0");
                    table.CheckConstraint("CK_AutonomousPrivateChatRounds_MessageCounts", "\"InitiatorMessageCount\" BETWEEN 0 AND 3 AND \"RecipientMessageCount\" BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_AutonomousPrivateChatRounds_Probability", "\"OccurrenceProbability\" IS NULL OR \"OccurrenceProbability\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_AutonomousPrivateChatRounds_RandomRoll", "\"RandomRoll\" IS NULL OR \"RandomRoll\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_AutonomousPrivateChatRounds_RoundNumber", "\"RoundNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_AutonomousPrivateChatRounds_AutonomousPrivateChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AutonomousPrivateChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrivateMessages_AutonomousPrivateChatRoundId",
                table: "PrivateMessages",
                column: "AutonomousPrivateChatRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateMessages_AutonomousPrivateChatSessionId_AutonomousSequenceNumber",
                table: "PrivateMessages",
                columns: new[] { "AutonomousPrivateChatSessionId", "AutonomousSequenceNumber" },
                unique: true,
                filter: "\"AutonomousSequenceNumber\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PrivateMessages_AutonomousRound_Consistency",
                table: "PrivateMessages",
                sql: "\"AutonomousPrivateChatRoundId\" IS NULL OR (\"AutonomousPrivateChatSessionId\" IS NOT NULL AND \"AutonomousSequenceNumber\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PrivateMessages_AutonomousSequence_Positive",
                table: "PrivateMessages",
                sql: "\"AutonomousSequenceNumber\" IS NULL OR \"AutonomousSequenceNumber\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousPrivateChatSessions_ContinuationRate",
                table: "AutonomousPrivateChatSessions",
                sql: "\"ContinuationRatePercent\" BETWEEN 0 AND 95");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousPrivateChatSessions_RoundLimits",
                table: "AutonomousPrivateChatSessions",
                sql: "\"MaximumRounds\" BETWEEN 1 AND 12 AND \"CompletedRounds\" BETWEEN 0 AND \"MaximumRounds\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousInteractionSettings_PrivateChatContinuationRate",
                table: "AutonomousInteractionSettings",
                sql: "\"PrivateChatContinuationRatePercent\" BETWEEN 0 AND 95");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousInteractionSettings_PrivateChatMaximumRounds",
                table: "AutonomousInteractionSettings",
                sql: "\"PrivateChatMaximumRounds\" BETWEEN 1 AND 12");

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousPrivateChatRounds_SessionId_RoundNumber",
                table: "AutonomousPrivateChatRounds",
                columns: new[] { "SessionId", "RoundNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PrivateMessages_AutonomousPrivateChatRounds_AutonomousPrivateChatRoundId",
                table: "PrivateMessages",
                column: "AutonomousPrivateChatRoundId",
                principalTable: "AutonomousPrivateChatRounds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrivateMessages_AutonomousPrivateChatRounds_AutonomousPrivateChatRoundId",
                table: "PrivateMessages");

            migrationBuilder.DropTable(
                name: "AutonomousPrivateChatRounds");

            migrationBuilder.DropIndex(
                name: "IX_PrivateMessages_AutonomousPrivateChatRoundId",
                table: "PrivateMessages");

            migrationBuilder.DropIndex(
                name: "IX_PrivateMessages_AutonomousPrivateChatSessionId_AutonomousSequenceNumber",
                table: "PrivateMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PrivateMessages_AutonomousRound_Consistency",
                table: "PrivateMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PrivateMessages_AutonomousSequence_Positive",
                table: "PrivateMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousPrivateChatSessions_ContinuationRate",
                table: "AutonomousPrivateChatSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousPrivateChatSessions_RoundLimits",
                table: "AutonomousPrivateChatSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousInteractionSettings_PrivateChatContinuationRate",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousInteractionSettings_PrivateChatMaximumRounds",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "AutonomousPrivateChatRoundId",
                table: "PrivateMessages");

            migrationBuilder.DropColumn(
                name: "AutonomousSequenceNumber",
                table: "PrivateMessages");

            migrationBuilder.DropColumn(
                name: "ContinuationRatePercent",
                table: "AutonomousPrivateChatSessions");

            migrationBuilder.DropColumn(
                name: "PrivateChatContinuationRatePercent",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "PrivateChatMaximumRounds",
                table: "AutonomousInteractionSettings");

            migrationBuilder.RenameColumn(
                name: "MaximumRounds",
                table: "AutonomousPrivateChatSessions",
                newName: "PlannedMaximumRounds");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousPrivateChatSessions_RoundLimits",
                table: "AutonomousPrivateChatSessions",
                sql: "\"PlannedMaximumRounds\" BETWEEN 1 AND 4 AND \"CompletedRounds\" BETWEEN 0 AND \"PlannedMaximumRounds\"");
        }
    }
}
