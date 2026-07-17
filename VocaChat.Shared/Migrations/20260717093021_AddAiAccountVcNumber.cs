using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAccountVcNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VcNumber",
                table: "AiAccounts",
                type: "TEXT",
                maxLength: 32,
                nullable: true,
                collation: "NOCASE");

            migrationBuilder.Sql(
                """
                WITH NumberedAccounts AS
                (
                    SELECT
                        "Id",
                        printf('%07d', 1000000 + ROW_NUMBER() OVER (
                            ORDER BY "CreatedAt", "Id")) AS "GeneratedVcNumber"
                    FROM "AiAccounts"
                )
                UPDATE "AiAccounts"
                SET "VcNumber" =
                (
                    SELECT "GeneratedVcNumber"
                    FROM NumberedAccounts
                    WHERE NumberedAccounts."Id" = "AiAccounts"."Id"
                );
                """);

            migrationBuilder.AlterColumn<string>(
                name: "VcNumber",
                table: "AiAccounts",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 32,
                oldNullable: true,
                oldCollation: "NOCASE");

            migrationBuilder.CreateIndex(
                name: "IX_AiAccounts_VcNumber",
                table: "AiAccounts",
                column: "VcNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiAccounts_VcNumber",
                table: "AiAccounts");

            migrationBuilder.DropColumn(
                name: "VcNumber",
                table: "AiAccounts");
        }
    }
}
