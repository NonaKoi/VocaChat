using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddRelationshipEvolutionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiRelationshipChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromAiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToAiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FamiliarityDelta = table.Column<int>(type: "INTEGER", nullable: false),
                    AffinityDelta = table.Column<int>(type: "INTEGER", nullable: false),
                    TrustDelta = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRelationshipChanges", x => x.Id);
                    table.CheckConstraint("CK_AiRelationshipChanges_AffinityDelta", "\"AffinityDelta\" BETWEEN -3 AND 3");
                    table.CheckConstraint("CK_AiRelationshipChanges_DifferentAccounts", "\"FromAiAccountId\" <> \"ToAiAccountId\"");
                    table.CheckConstraint("CK_AiRelationshipChanges_FamiliarityDelta", "\"FamiliarityDelta\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_AiRelationshipChanges_TrustDelta", "\"TrustDelta\" BETWEEN -2 AND 2");
                    table.ForeignKey(
                        name: "FK_AiRelationshipChanges_AiAccounts_FromAiAccountId",
                        column: x => x.FromAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiRelationshipChanges_AiAccounts_ToAiAccountId",
                        column: x => x.ToAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiRelationshipChanges_AutonomousPrivateChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AutonomousPrivateChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiRelationshipChanges_FromAiAccountId_CreatedAt",
                table: "AiRelationshipChanges",
                columns: new[] { "FromAiAccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiRelationshipChanges_SessionId_FromAiAccountId_ToAiAccountId",
                table: "AiRelationshipChanges",
                columns: new[] { "SessionId", "FromAiAccountId", "ToAiAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiRelationshipChanges_ToAiAccountId",
                table: "AiRelationshipChanges",
                column: "ToAiAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiRelationshipChanges");
        }
    }
}
