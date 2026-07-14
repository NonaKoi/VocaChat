using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.ConsoleApp.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GroupMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GroupChatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SenderType = table.Column<int>(type: "INTEGER", nullable: false),
                    SenderDisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SenderAiAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Content = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMessages", x => x.Id);
                    table.CheckConstraint("CK_GroupMessages_Content_MaxLength", "length(\"Content\") <= 4000");
                    table.CheckConstraint("CK_GroupMessages_Content_NotBlank", "length(trim(\"Content\")) > 0");
                    table.CheckConstraint("CK_GroupMessages_Sender_Consistency", "(\"SenderType\" = 0 AND \"SenderAiAccountId\" IS NULL) OR (\"SenderType\" = 1 AND \"SenderAiAccountId\" IS NOT NULL)");
                    table.CheckConstraint("CK_GroupMessages_SenderDisplayName_MaxLength", "length(\"SenderDisplayName\") <= 100");
                    table.CheckConstraint("CK_GroupMessages_SenderDisplayName_NotBlank", "length(trim(\"SenderDisplayName\")) > 0");
                    table.ForeignKey(
                        name: "FK_GroupMessages_AiAccounts_SenderAiAccountId",
                        column: x => x.SenderAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupMessages_GroupChats_GroupChatId",
                        column: x => x.GroupChatId,
                        principalTable: "GroupChats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_GroupChatId_SentAt_Id",
                table: "GroupMessages",
                columns: new[] { "GroupChatId", "SentAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_SenderAiAccountId",
                table: "GroupMessages",
                column: "SenderAiAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupMessages");
        }
    }
}
