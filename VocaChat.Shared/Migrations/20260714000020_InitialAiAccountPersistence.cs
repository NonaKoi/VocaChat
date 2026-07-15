using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class InitialAiAccountPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    IdentityDescription = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Personality = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SpeakingStyle = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAccounts", x => x.Id);
                    table.CheckConstraint("CK_AiAccounts_IdentityDescription_MaxLength", "length(\"IdentityDescription\") <= 500");
                    table.CheckConstraint("CK_AiAccounts_Nickname_MaxLength", "length(\"Nickname\") <= 50");
                    table.CheckConstraint("CK_AiAccounts_Nickname_NotBlank", "length(trim(\"Nickname\")) > 0");
                    table.CheckConstraint("CK_AiAccounts_Personality_MaxLength", "length(\"Personality\") <= 200");
                    table.CheckConstraint("CK_AiAccounts_SpeakingStyle_MaxLength", "length(\"SpeakingStyle\") <= 200");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAccounts_Nickname",
                table: "AiAccounts",
                column: "Nickname",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAccounts");
        }
    }
}
