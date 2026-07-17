using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddContactsAndPrivateChats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContactGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactGroups", x => x.Id);
                    table.CheckConstraint("CK_ContactGroups_Name_MaxLength", "length(\"Name\") <= 50");
                    table.CheckConstraint("CK_ContactGroups_Name_NotBlank", "length(trim(\"Name\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContactGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contacts_AiAccounts_AiAccountId",
                        column: x => x.AiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contacts_ContactGroups_ContactGroupId",
                        column: x => x.ContactGroupId,
                        principalTable: "ContactGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrivateChats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContactId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivateChats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrivateChats_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrivateMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PrivateChatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SenderType = table.Column<int>(type: "INTEGER", nullable: false),
                    SenderDisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SenderAiAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Content = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivateMessages", x => x.Id);
                    table.CheckConstraint("CK_PrivateMessages_Content_NotBlank", "length(trim(\"Content\")) > 0");
                    table.CheckConstraint("CK_PrivateMessages_Sender_Consistency", "(\"SenderType\" = 0 AND \"SenderAiAccountId\" IS NULL) OR (\"SenderType\" = 1 AND \"SenderAiAccountId\" IS NOT NULL)");
                    table.CheckConstraint("CK_PrivateMessages_SenderDisplayName_NotBlank", "length(trim(\"SenderDisplayName\")) > 0");
                    table.ForeignKey(
                        name: "FK_PrivateMessages_AiAccounts_SenderAiAccountId",
                        column: x => x.SenderAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrivateMessages_PrivateChats_PrivateChatId",
                        column: x => x.PrivateChatId,
                        principalTable: "PrivateChats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ContactGroups",
                columns: new[] { "Id", "CreatedAt", "Name", "SortOrder" },
                values: new object[] { new Guid("6d53f7a8-2d25-4e8a-bbad-82dc67384f01"), new DateTime(2026, 7, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), "默认分组", 0 });

            migrationBuilder.Sql(
                """
                INSERT INTO "Contacts" ("Id", "AiAccountId", "ContactGroupId", "CreatedAt")
                SELECT
                    upper(
                    lower(hex(randomblob(4))) || '-' ||
                    lower(hex(randomblob(2))) || '-' ||
                    '4' || substr(lower(hex(randomblob(2))), 2) || '-' ||
                    substr('89ab', abs(random()) % 4 + 1, 1) ||
                    substr(lower(hex(randomblob(2))), 2) || '-' ||
                    lower(hex(randomblob(6)))),
                    "Id",
                    '6D53F7A8-2D25-4E8A-BBAD-82DC67384F01',
                    "CreatedAt"
                FROM "AiAccounts";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ContactGroups_Name",
                table: "ContactGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_AiAccountId",
                table: "Contacts",
                column: "AiAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_ContactGroupId",
                table: "Contacts",
                column: "ContactGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateChats_ContactId",
                table: "PrivateChats",
                column: "ContactId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrivateMessages_PrivateChatId_SentAt_Id",
                table: "PrivateMessages",
                columns: new[] { "PrivateChatId", "SentAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivateMessages_SenderAiAccountId",
                table: "PrivateMessages",
                column: "SenderAiAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrivateMessages");

            migrationBuilder.DropTable(
                name: "PrivateChats");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropTable(
                name: "ContactGroups");
        }
    }
}
