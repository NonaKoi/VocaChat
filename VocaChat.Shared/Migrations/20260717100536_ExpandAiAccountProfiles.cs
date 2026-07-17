using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class ExpandAiAccountProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "Birthday",
                table: "AiAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "AiAccounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Hometown",
                table: "AiAccounts",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "AiAccounts",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                table: "AiAccounts",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "OnlineStatus",
                table: "AiAccounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Signature",
                table: "AiAccounts",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AiAccountTags",
                columns: table => new
                {
                    AiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAccountTags", x => new { x.AiAccountId, x.Type, x.Value });
                    table.ForeignKey(
                        name: "FK_AiAccountTags_AiAccounts_AiAccountId",
                        column: x => x.AiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAccountTags_Type_Value",
                table: "AiAccountTags",
                columns: new[] { "Type", "Value" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAccountTags");

            migrationBuilder.DropColumn(
                name: "Birthday",
                table: "AiAccounts");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "AiAccounts");

            migrationBuilder.DropColumn(
                name: "Hometown",
                table: "AiAccounts");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "AiAccounts");

            migrationBuilder.DropColumn(
                name: "Occupation",
                table: "AiAccounts");

            migrationBuilder.DropColumn(
                name: "OnlineStatus",
                table: "AiAccounts");

            migrationBuilder.DropColumn(
                name: "Signature",
                table: "AiAccounts");
        }
    }
}
