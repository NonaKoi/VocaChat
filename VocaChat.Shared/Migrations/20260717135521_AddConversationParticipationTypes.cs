using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationParticipationTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PrivateChats_ContactId",
                table: "PrivateChats");

            migrationBuilder.AlterColumn<Guid>(
                name: "ContactId",
                table: "PrivateChats",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "FirstAiAccountId",
                table: "PrivateChats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "PrivateChats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SecondAiAccountId",
                table: "PrivateChats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesLocalUser",
                table: "GroupChats",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrivateChats_ContactId",
                table: "PrivateChats",
                column: "ContactId",
                unique: true,
                filter: "\"ContactId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateChats_FirstAiAccountId_SecondAiAccountId",
                table: "PrivateChats",
                columns: new[] { "FirstAiAccountId", "SecondAiAccountId" },
                unique: true,
                filter: "\"FirstAiAccountId\" IS NOT NULL AND \"SecondAiAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateChats_SecondAiAccountId",
                table: "PrivateChats",
                column: "SecondAiAccountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PrivateChats_Participants_Consistency",
                table: "PrivateChats",
                sql: "(\"Kind\" = 0 AND \"ContactId\" IS NOT NULL AND \"FirstAiAccountId\" IS NULL AND \"SecondAiAccountId\" IS NULL) OR (\"Kind\" = 1 AND \"ContactId\" IS NULL AND \"FirstAiAccountId\" IS NOT NULL AND \"SecondAiAccountId\" IS NOT NULL AND \"FirstAiAccountId\" <> \"SecondAiAccountId\")");

            migrationBuilder.AddForeignKey(
                name: "FK_PrivateChats_AiAccounts_FirstAiAccountId",
                table: "PrivateChats",
                column: "FirstAiAccountId",
                principalTable: "AiAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrivateChats_AiAccounts_SecondAiAccountId",
                table: "PrivateChats",
                column: "SecondAiAccountId",
                principalTable: "AiAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrivateChats_AiAccounts_FirstAiAccountId",
                table: "PrivateChats");

            migrationBuilder.DropForeignKey(
                name: "FK_PrivateChats_AiAccounts_SecondAiAccountId",
                table: "PrivateChats");

            migrationBuilder.DropIndex(
                name: "IX_PrivateChats_ContactId",
                table: "PrivateChats");

            migrationBuilder.DropIndex(
                name: "IX_PrivateChats_FirstAiAccountId_SecondAiAccountId",
                table: "PrivateChats");

            migrationBuilder.DropIndex(
                name: "IX_PrivateChats_SecondAiAccountId",
                table: "PrivateChats");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PrivateChats_Participants_Consistency",
                table: "PrivateChats");

            migrationBuilder.DropColumn(
                name: "FirstAiAccountId",
                table: "PrivateChats");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "PrivateChats");

            migrationBuilder.DropColumn(
                name: "SecondAiAccountId",
                table: "PrivateChats");

            migrationBuilder.DropColumn(
                name: "IncludesLocalUser",
                table: "GroupChats");

            migrationBuilder.AlterColumn<Guid>(
                name: "ContactId",
                table: "PrivateChats",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrivateChats_ContactId",
                table: "PrivateChats",
                column: "ContactId",
                unique: true);
        }
    }
}
