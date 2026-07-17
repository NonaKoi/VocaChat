using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAccountAutonomySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiAccountAutonomySettings",
                columns: table => new
                {
                    AiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    InitiativeLevel = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CanInitiatePrivateChats = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanInitiateGroupChats = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanJoinGroupChats = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAccountAutonomySettings", x => x.AiAccountId);
                    table.CheckConstraint("CK_AiAccountAutonomySettings_InitiativeLevel", "\"InitiativeLevel\" IN ('Low', 'Normal', 'High')");
                    table.ForeignKey(
                        name: "FK_AiAccountAutonomySettings_AiAccounts_AiAccountId",
                        column: x => x.AiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAccountAutonomySettings");
        }
    }
}
