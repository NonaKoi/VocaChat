using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAiMemories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiMemories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerAiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectAiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Salience = table.Column<int>(type: "INTEGER", nullable: false),
                    SourcePrivateChatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastRecalledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMemories", x => x.Id);
                    table.CheckConstraint("CK_AiMemories_DifferentAccounts", "\"OwnerAiAccountId\" <> \"SubjectAiAccountId\"");
                    table.CheckConstraint("CK_AiMemories_Salience", "\"Salience\" BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_AiMemories_Summary", "length(trim(\"Summary\")) > 0");
                    table.CheckConstraint("CK_AiMemories_Type", "\"Type\" BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_AiMemories_AiAccounts_OwnerAiAccountId",
                        column: x => x.OwnerAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiMemories_AiAccounts_SubjectAiAccountId",
                        column: x => x.SubjectAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiMemories_AutonomousPrivateChatSessions_SourceSessionId",
                        column: x => x.SourceSessionId,
                        principalTable: "AutonomousPrivateChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiMemories_PrivateChats_SourcePrivateChatId",
                        column: x => x.SourcePrivateChatId,
                        principalTable: "PrivateChats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiMemories_OwnerAiAccountId_SubjectAiAccountId_IsActive_Salience_OccurredAt",
                table: "AiMemories",
                columns: new[] { "OwnerAiAccountId", "SubjectAiAccountId", "IsActive", "Salience", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiMemories_SourcePrivateChatId",
                table: "AiMemories",
                column: "SourcePrivateChatId");

            migrationBuilder.CreateIndex(
                name: "IX_AiMemories_SourceSessionId_OwnerAiAccountId_SubjectAiAccountId_Type_Summary",
                table: "AiMemories",
                columns: new[] { "SourceSessionId", "OwnerAiAccountId", "SubjectAiAccountId", "Type", "Summary" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiMemories_SubjectAiAccountId",
                table: "AiMemories",
                column: "SubjectAiAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiMemories");
        }
    }
}
