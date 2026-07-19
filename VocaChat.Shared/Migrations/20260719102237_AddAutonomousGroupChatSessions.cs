using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAutonomousGroupChatSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AutonomousGroupChatSessionId",
                table: "GroupMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AutonomousGroupChatSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GroupChatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InitiatorAiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    EndReason = table.Column<int>(type: "INTEGER", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomousGroupChatSessions", x => x.Id);
                    table.CheckConstraint("CK_AutonomousGroupChatSessions_StateConsistency", "(\"Status\" = 0 AND \"EndReason\" IS NULL AND \"EndedAt\" IS NULL) OR (\"Status\" IN (1, 2) AND \"EndReason\" IS NOT NULL AND \"EndedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AutonomousGroupChatSessions_AiAccounts_InitiatorAiAccountId",
                        column: x => x.InitiatorAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutonomousGroupChatSessions_GroupChats_GroupChatId",
                        column: x => x.GroupChatId,
                        principalTable: "GroupChats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutonomousGroupChatSessionParticipants",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AiAccountId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomousGroupChatSessionParticipants", x => new { x.SessionId, x.AiAccountId });
                    table.ForeignKey(
                        name: "FK_AutonomousGroupChatSessionParticipants_AiAccounts_AiAccountId",
                        column: x => x.AiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutonomousGroupChatSessionParticipants_AutonomousGroupChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AutonomousGroupChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_AutonomousGroupChatSessionId",
                table: "GroupMessages",
                column: "AutonomousGroupChatSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousGroupChatSessionParticipants_AiAccountId",
                table: "AutonomousGroupChatSessionParticipants",
                column: "AiAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousGroupChatSessions_GroupChatId",
                table: "AutonomousGroupChatSessions",
                column: "GroupChatId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousGroupChatSessions_GroupChatId_StartedAt",
                table: "AutonomousGroupChatSessions",
                columns: new[] { "GroupChatId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousGroupChatSessions_InitiatorAiAccountId",
                table: "AutonomousGroupChatSessions",
                column: "InitiatorAiAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousGroupChatSessions_Status_LastActivityAt",
                table: "AutonomousGroupChatSessions",
                columns: new[] { "Status", "LastActivityAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMessages_AutonomousGroupChatSessions_AutonomousGroupChatSessionId",
                table: "GroupMessages",
                column: "AutonomousGroupChatSessionId",
                principalTable: "AutonomousGroupChatSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupMessages_AutonomousGroupChatSessions_AutonomousGroupChatSessionId",
                table: "GroupMessages");

            migrationBuilder.DropTable(
                name: "AutonomousGroupChatSessionParticipants");

            migrationBuilder.DropTable(
                name: "AutonomousGroupChatSessions");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_AutonomousGroupChatSessionId",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "AutonomousGroupChatSessionId",
                table: "GroupMessages");
        }
    }
}
