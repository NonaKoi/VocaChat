using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAiSelfMemories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiSelfMemories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, collation: "NOCASE"),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Salience = table.Column<int>(type: "INTEGER", nullable: false),
                    IsUserLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSelfMemories", x => x.Id);
                    table.CheckConstraint("CK_AiSelfMemories_Salience", "\"Salience\" BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_AiSelfMemories_Source", "\"Source\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_AiSelfMemories_Status", "\"Status\" BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_AiSelfMemories_Summary", "length(trim(\"Summary\")) > 0");
                    table.CheckConstraint("CK_AiSelfMemories_Type", "\"Type\" BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_AiSelfMemories_Validity", "\"ValidFrom\" IS NULL OR \"ValidUntil\" IS NULL OR \"ValidUntil\" >= \"ValidFrom\"");
                    table.ForeignKey(
                        name: "FK_AiSelfMemories_AiAccounts_AiAccountId",
                        column: x => x.AiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiSelfMemories_AiAccountId_Status_Salience_UpdatedAt",
                table: "AiSelfMemories",
                columns: new[] { "AiAccountId", "Status", "Salience", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiSelfMemories_AiAccountId_Type_Summary",
                table: "AiSelfMemories",
                columns: new[] { "AiAccountId", "Type", "Summary" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AiSelfMemories_SourceMessageId",
                table: "AiSelfMemories",
                column: "SourceMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiSelfMemories");
        }
    }
}
