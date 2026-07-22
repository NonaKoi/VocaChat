using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupMessageInteractionLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InteractionBatchId",
                table: "GroupMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToMessageId",
                table: "GroupMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_GroupChatId_InteractionBatchId",
                table: "GroupMessages",
                columns: new[] { "GroupChatId", "InteractionBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_ReplyToMessageId",
                table: "GroupMessages",
                column: "ReplyToMessageId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GroupMessages_ReplyTarget_NotSelf",
                table: "GroupMessages",
                sql: "\"ReplyToMessageId\" IS NULL OR \"ReplyToMessageId\" <> \"Id\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GroupMessages_ReplyTarget_RequiresBatch",
                table: "GroupMessages",
                sql: "\"ReplyToMessageId\" IS NULL OR \"InteractionBatchId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMessages_GroupMessages_ReplyToMessageId",
                table: "GroupMessages",
                column: "ReplyToMessageId",
                principalTable: "GroupMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupMessages_GroupMessages_ReplyToMessageId",
                table: "GroupMessages");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_GroupChatId_InteractionBatchId",
                table: "GroupMessages");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_ReplyToMessageId",
                table: "GroupMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GroupMessages_ReplyTarget_NotSelf",
                table: "GroupMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GroupMessages_ReplyTarget_RequiresBatch",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "InteractionBatchId",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "GroupMessages");
        }
    }
}
