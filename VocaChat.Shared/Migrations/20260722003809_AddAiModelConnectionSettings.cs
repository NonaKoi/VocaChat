using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAiModelConnectionSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiAccountModelConnectionSettings",
                columns: table => new
                {
                    AiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UseGlobalSettings = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProtectedApiKey = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAccountModelConnectionSettings", x => x.AiAccountId);
                    table.ForeignKey(
                        name: "FK_AiAccountModelConnectionSettings_AiAccounts_AiAccountId",
                        column: x => x.AiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiModelConnectionSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProtectedApiKey = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModelConnectionSettings", x => x.Id);
                    table.CheckConstraint("CK_AiModelConnectionSettings_Singleton", "\"Id\" = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAccountModelConnectionSettings");

            migrationBuilder.DropTable(
                name: "AiModelConnectionSettings");
        }
    }
}
