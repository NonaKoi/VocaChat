using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAiModelTokenUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AiResponseBatchId",
                table: "PrivateMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AiResponseBatchId",
                table: "GroupMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiModelInvocationUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Stage = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AiAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    GroupChatId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PrivateChatId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AutonomousPrivateChatSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AutonomousGroupChatSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    InteractionBatchId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AiResponseBatchId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AttemptNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PromptTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    CompletionTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    PromptCacheHitTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    PromptCacheMissTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    ReasoningTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    UsageReported = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModelInvocationUsages", x => x.Id);
                    table.CheckConstraint("CK_AiModelInvocationUsages_AttemptNumber_Positive", "\"AttemptNumber\" > 0");
                    table.CheckConstraint("CK_AiModelInvocationUsages_Tokens_NonNegative", "(\"PromptTokens\" IS NULL OR \"PromptTokens\" >= 0) AND (\"CompletionTokens\" IS NULL OR \"CompletionTokens\" >= 0) AND (\"TotalTokens\" IS NULL OR \"TotalTokens\" >= 0) AND (\"PromptCacheHitTokens\" IS NULL OR \"PromptCacheHitTokens\" >= 0) AND (\"PromptCacheMissTokens\" IS NULL OR \"PromptCacheMissTokens\" >= 0) AND (\"ReasoningTokens\" IS NULL OR \"ReasoningTokens\" >= 0)");
                    table.CheckConstraint("CK_AiModelInvocationUsages_UsageReported", "\"UsageReported\" = 0 OR (\"PromptTokens\" IS NOT NULL AND \"CompletionTokens\" IS NOT NULL AND \"TotalTokens\" IS NOT NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrivateMessages_PrivateChatId_AiResponseBatchId",
                table: "PrivateMessages",
                columns: new[] { "PrivateChatId", "AiResponseBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_GroupChatId_AiResponseBatchId",
                table: "GroupMessages",
                columns: new[] { "GroupChatId", "AiResponseBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiModelInvocationUsages_AiResponseBatchId",
                table: "AiModelInvocationUsages",
                column: "AiResponseBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AiModelInvocationUsages_AutonomousGroupChatSessionId",
                table: "AiModelInvocationUsages",
                column: "AutonomousGroupChatSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AiModelInvocationUsages_AutonomousPrivateChatSessionId",
                table: "AiModelInvocationUsages",
                column: "AutonomousPrivateChatSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AiModelInvocationUsages_GroupChatId_RecordedAt",
                table: "AiModelInvocationUsages",
                columns: new[] { "GroupChatId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiModelInvocationUsages_InteractionBatchId",
                table: "AiModelInvocationUsages",
                column: "InteractionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AiModelInvocationUsages_PrivateChatId_RecordedAt",
                table: "AiModelInvocationUsages",
                columns: new[] { "PrivateChatId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiModelInvocationUsages");

            migrationBuilder.DropIndex(
                name: "IX_PrivateMessages_PrivateChatId_AiResponseBatchId",
                table: "PrivateMessages");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_GroupChatId_AiResponseBatchId",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "AiResponseBatchId",
                table: "PrivateMessages");

            migrationBuilder.DropColumn(
                name: "AiResponseBatchId",
                table: "GroupMessages");
        }
    }
}
