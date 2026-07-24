using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterWorlds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CharacterWorldId",
                table: "AiAccounts",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("2d215860-2e59-4b55-916c-fc5cb6e96c27"));

            migrationBuilder.CreateTable(
                name: "CharacterWorlds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterWorlds", x => x.Id);
                    table.CheckConstraint("CK_CharacterWorlds_Description_MaxLength", "length(\"Description\") <= 4000");
                    table.CheckConstraint("CK_CharacterWorlds_Name_MaxLength", "length(\"Name\") <= 100");
                    table.CheckConstraint("CK_CharacterWorlds_Name_NotBlank", "length(trim(\"Name\")) > 0");
                });

            migrationBuilder.InsertData(
                table: "CharacterWorlds",
                columns: new[] { "Id", "CreatedAt", "Description", "Name", "UpdatedAt" },
                values: new object[] { new Guid("2d215860-2e59-4b55-916c-fc5cb6e96c27"), new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc), "采用现代现实社会的基本规则；未经用户或可靠来源确认的时效性外部信息不得作为确定事实。", "现实世界", new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_AiAccounts_CharacterWorldId",
                table: "AiAccounts",
                column: "CharacterWorldId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterWorlds_Name",
                table: "CharacterWorlds",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AiAccounts_CharacterWorlds_CharacterWorldId",
                table: "AiAccounts",
                column: "CharacterWorldId",
                principalTable: "CharacterWorlds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiAccounts_CharacterWorlds_CharacterWorldId",
                table: "AiAccounts");

            migrationBuilder.DropTable(
                name: "CharacterWorlds");

            migrationBuilder.DropIndex(
                name: "IX_AiAccounts_CharacterWorldId",
                table: "AiAccounts");

            migrationBuilder.DropColumn(
                name: "CharacterWorldId",
                table: "AiAccounts");
        }
    }
}
