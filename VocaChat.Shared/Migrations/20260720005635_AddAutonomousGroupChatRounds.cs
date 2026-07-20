using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAutonomousGroupChatRounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AutonomousGroupChatRoundId",
                table: "GroupMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupChatContinuationRatePercent",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 80);

            migrationBuilder.AddColumn<int>(
                name: "GroupChatMaximumRounds",
                table: "AutonomousInteractionSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<int>(
                name: "CompletedRounds",
                table: "AutonomousGroupChatSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ContinuationRatePercent",
                table: "AutonomousGroupChatSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 80);

            migrationBuilder.AddColumn<int>(
                name: "MaximumRounds",
                table: "AutonomousGroupChatSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.CreateTable(
                name: "AutonomousGroupChatRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoundNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    IsClosing = table.Column<bool>(type: "INTEGER", nullable: false),
                    OccurrenceProbability = table.Column<double>(type: "REAL", nullable: true),
                    RandomRoll = table.Column<double>(type: "REAL", nullable: true),
                    PlannedSpeakerCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PlannedMessageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomousGroupChatRounds", x => x.Id);
                    table.CheckConstraint("CK_AutonomousGroupChatRounds_ClosingProbability", "(\"IsClosing\" = 1 AND \"OccurrenceProbability\" IS NULL AND \"RandomRoll\" IS NULL) OR \"IsClosing\" = 0");
                    table.CheckConstraint("CK_AutonomousGroupChatRounds_MessageCount", "\"PlannedMessageCount\" BETWEEN 0 AND 9");
                    table.CheckConstraint("CK_AutonomousGroupChatRounds_NormalRoundHasMessages", "\"IsClosing\" = 1 OR (\"PlannedSpeakerCount\" > 0 AND \"PlannedMessageCount\" >= \"PlannedSpeakerCount\")");
                    table.CheckConstraint("CK_AutonomousGroupChatRounds_Probability", "\"OccurrenceProbability\" IS NULL OR \"OccurrenceProbability\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_AutonomousGroupChatRounds_RandomRoll", "\"RandomRoll\" IS NULL OR \"RandomRoll\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_AutonomousGroupChatRounds_RoundNumber", "\"RoundNumber\" > 0");
                    table.CheckConstraint("CK_AutonomousGroupChatRounds_SpeakerCount", "\"PlannedSpeakerCount\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_AutonomousGroupChatRounds_AutonomousGroupChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AutonomousGroupChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_AutonomousGroupChatRoundId",
                table: "GroupMessages",
                column: "AutonomousGroupChatRoundId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GroupMessages_AutonomousRound_Consistency",
                table: "GroupMessages",
                sql: "\"AutonomousGroupChatRoundId\" IS NULL OR \"AutonomousGroupChatSessionId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousInteractionSettings_GroupChatContinuationRate",
                table: "AutonomousInteractionSettings",
                sql: "\"GroupChatContinuationRatePercent\" BETWEEN 0 AND 95");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousInteractionSettings_GroupChatMaximumRounds",
                table: "AutonomousInteractionSettings",
                sql: "\"GroupChatMaximumRounds\" BETWEEN 1 AND 12");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousGroupChatSessions_CompletedRounds",
                table: "AutonomousGroupChatSessions",
                sql: "\"CompletedRounds\" >= 0 AND \"CompletedRounds\" <= \"MaximumRounds\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousGroupChatSessions_ContinuationRate",
                table: "AutonomousGroupChatSessions",
                sql: "\"ContinuationRatePercent\" BETWEEN 0 AND 95");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AutonomousGroupChatSessions_MaximumRounds",
                table: "AutonomousGroupChatSessions",
                sql: "\"MaximumRounds\" BETWEEN 1 AND 12");

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousGroupChatRounds_SessionId_RoundNumber",
                table: "AutonomousGroupChatRounds",
                columns: new[] { "SessionId", "RoundNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMessages_AutonomousGroupChatRounds_AutonomousGroupChatRoundId",
                table: "GroupMessages",
                column: "AutonomousGroupChatRoundId",
                principalTable: "AutonomousGroupChatRounds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupMessages_AutonomousGroupChatRounds_AutonomousGroupChatRoundId",
                table: "GroupMessages");

            migrationBuilder.DropTable(
                name: "AutonomousGroupChatRounds");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_AutonomousGroupChatRoundId",
                table: "GroupMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GroupMessages_AutonomousRound_Consistency",
                table: "GroupMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousInteractionSettings_GroupChatContinuationRate",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousInteractionSettings_GroupChatMaximumRounds",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousGroupChatSessions_CompletedRounds",
                table: "AutonomousGroupChatSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousGroupChatSessions_ContinuationRate",
                table: "AutonomousGroupChatSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AutonomousGroupChatSessions_MaximumRounds",
                table: "AutonomousGroupChatSessions");

            migrationBuilder.DropColumn(
                name: "AutonomousGroupChatRoundId",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "GroupChatContinuationRatePercent",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "GroupChatMaximumRounds",
                table: "AutonomousInteractionSettings");

            migrationBuilder.DropColumn(
                name: "CompletedRounds",
                table: "AutonomousGroupChatSessions");

            migrationBuilder.DropColumn(
                name: "ContinuationRatePercent",
                table: "AutonomousGroupChatSessions");

            migrationBuilder.DropColumn(
                name: "MaximumRounds",
                table: "AutonomousGroupChatSessions");
        }
    }
}
