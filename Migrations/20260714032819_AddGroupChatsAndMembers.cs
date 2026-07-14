using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.ConsoleApp.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupChatsAndMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GroupChats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupChats", x => x.Id);
                    table.CheckConstraint("CK_GroupChats_Name_MaxLength", "length(\"Name\") <= 100");
                    table.CheckConstraint("CK_GroupChats_Name_NotBlank", "length(trim(\"Name\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "GroupChatMembers",
                columns: table => new
                {
                    GroupChatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AiAccountId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupChatMembers", x => new { x.GroupChatId, x.AiAccountId });
                    table.ForeignKey(
                        name: "FK_GroupChatMembers_AiAccounts_AiAccountId",
                        column: x => x.AiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupChatMembers_GroupChats_GroupChatId",
                        column: x => x.GroupChatId,
                        principalTable: "GroupChats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupChatMembers_AiAccountId",
                table: "GroupChatMembers",
                column: "AiAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupChatMembers");

            migrationBuilder.DropTable(
                name: "GroupChats");
        }
    }
}
