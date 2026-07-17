using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAiRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiRelationships",
                columns: table => new
                {
                    FromAiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToAiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Familiarity = table.Column<int>(type: "INTEGER", nullable: false),
                    Affinity = table.Column<int>(type: "INTEGER", nullable: false),
                    Trust = table.Column<int>(type: "INTEGER", nullable: false),
                    InteractionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastInteractionAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRelationships", x => new { x.FromAiAccountId, x.ToAiAccountId });
                    table.CheckConstraint("CK_AiRelationships_Affinity", "\"Affinity\" BETWEEN -100 AND 100");
                    table.CheckConstraint("CK_AiRelationships_DifferentAccounts", "\"FromAiAccountId\" <> \"ToAiAccountId\"");
                    table.CheckConstraint("CK_AiRelationships_Familiarity", "\"Familiarity\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_AiRelationships_InteractionCount", "\"InteractionCount\" >= 0");
                    table.CheckConstraint("CK_AiRelationships_Trust", "\"Trust\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_AiRelationships_AiAccounts_FromAiAccountId",
                        column: x => x.FromAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiRelationships_AiAccounts_ToAiAccountId",
                        column: x => x.ToAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiRelationships_ToAiAccountId",
                table: "AiRelationships",
                column: "ToAiAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiRelationships");
        }
    }
}
