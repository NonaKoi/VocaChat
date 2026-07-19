using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAutonomousPrivateChatSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AutonomousPrivateChatSessionId",
                table: "PrivateMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AutonomousPrivateChatSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PrivateChatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InitiatorAiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientAiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PlannedMaximumRounds = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedRounds = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    EndReason = table.Column<int>(type: "INTEGER", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomousPrivateChatSessions", x => x.Id);
                    table.CheckConstraint("CK_AutonomousPrivateChatSessions_DifferentParticipants", "\"InitiatorAiAccountId\" <> \"RecipientAiAccountId\"");
                    table.CheckConstraint("CK_AutonomousPrivateChatSessions_RoundLimits", "\"PlannedMaximumRounds\" BETWEEN 1 AND 4 AND \"CompletedRounds\" BETWEEN 0 AND \"PlannedMaximumRounds\"");
                    table.CheckConstraint("CK_AutonomousPrivateChatSessions_StateConsistency", "(\"Status\" = 0 AND \"EndReason\" IS NULL AND \"EndedAt\" IS NULL) OR (\"Status\" IN (1, 2, 3) AND \"EndReason\" IS NOT NULL AND \"EndedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AutonomousPrivateChatSessions_AiAccounts_InitiatorAiAccountId",
                        column: x => x.InitiatorAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutonomousPrivateChatSessions_AiAccounts_RecipientAiAccountId",
                        column: x => x.RecipientAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutonomousPrivateChatSessions_PrivateChats_PrivateChatId",
                        column: x => x.PrivateChatId,
                        principalTable: "PrivateChats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrivateMessages_AutonomousPrivateChatSessionId_SentAt_Id",
                table: "PrivateMessages",
                columns: new[] { "AutonomousPrivateChatSessionId", "SentAt", "Id" },
                filter: "\"AutonomousPrivateChatSessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousPrivateChatSessions_InitiatorAiAccountId",
                table: "AutonomousPrivateChatSessions",
                column: "InitiatorAiAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousPrivateChatSessions_PrivateChatId",
                table: "AutonomousPrivateChatSessions",
                column: "PrivateChatId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousPrivateChatSessions_PrivateChatId_StartedAt",
                table: "AutonomousPrivateChatSessions",
                columns: new[] { "PrivateChatId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousPrivateChatSessions_RecipientAiAccountId",
                table: "AutonomousPrivateChatSessions",
                column: "RecipientAiAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousPrivateChatSessions_Status_LastActivityAt",
                table: "AutonomousPrivateChatSessions",
                columns: new[] { "Status", "LastActivityAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_PrivateMessages_AutonomousPrivateChatSessions_AutonomousPrivateChatSessionId",
                table: "PrivateMessages",
                column: "AutonomousPrivateChatSessionId",
                principalTable: "AutonomousPrivateChatSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrivateMessages_AutonomousPrivateChatSessions_AutonomousPrivateChatSessionId",
                table: "PrivateMessages");

            migrationBuilder.DropTable(
                name: "AutonomousPrivateChatSessions");

            migrationBuilder.DropIndex(
                name: "IX_PrivateMessages_AutonomousPrivateChatSessionId_SentAt_Id",
                table: "PrivateMessages");

            migrationBuilder.DropColumn(
                name: "AutonomousPrivateChatSessionId",
                table: "PrivateMessages");
        }
    }
}
