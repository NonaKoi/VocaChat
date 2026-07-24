using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddWorldKnowledgeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AiWorldKnowledge_Status",
                table: "AiWorldKnowledge");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiWorldKnowledge_Status",
                table: "AiWorldKnowledge",
                sql: "\"Status\" BETWEEN 0 AND 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AiWorldKnowledge_Status",
                table: "AiWorldKnowledge");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiWorldKnowledge_Status",
                table: "AiWorldKnowledge",
                sql: "\"Status\" BETWEEN 0 AND 2");
        }
    }
}
