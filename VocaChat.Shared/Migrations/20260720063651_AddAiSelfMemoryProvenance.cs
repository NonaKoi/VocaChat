using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAiSelfMemoryProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceConversationId",
                table: "AiSelfMemories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiSelfMemories_AiAccountId_SourceMessageId_Type_Summary",
                table: "AiSelfMemories",
                columns: new[] { "AiAccountId", "SourceMessageId", "Type", "Summary" },
                unique: true,
                filter: "\"Source\" = 1 AND \"SourceMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AiSelfMemories_SourceConversationId",
                table: "AiSelfMemories",
                column: "SourceConversationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiSelfMemories_AiAccountId_SourceMessageId_Type_Summary",
                table: "AiSelfMemories");

            migrationBuilder.DropIndex(
                name: "IX_AiSelfMemories_SourceConversationId",
                table: "AiSelfMemories");

            migrationBuilder.DropColumn(
                name: "SourceConversationId",
                table: "AiSelfMemories");
        }
    }
}
