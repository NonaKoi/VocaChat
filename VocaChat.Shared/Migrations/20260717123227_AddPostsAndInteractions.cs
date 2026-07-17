using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddPostsAndInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Posts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AuthorAiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Posts", x => x.Id);
                    table.CheckConstraint("CK_Posts_Content_MaxLength", "length(\"Content\") <= 2000");
                    table.CheckConstraint("CK_Posts_Content_NotBlank", "length(trim(\"Content\")) > 0");
                    table.ForeignKey(
                        name: "FK_Posts_AiAccounts_AuthorAiAccountId",
                        column: x => x.AuthorAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PostComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AiAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SenderDisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostComments", x => x.Id);
                    table.CheckConstraint("CK_PostComments_Content_NotBlank", "length(trim(\"Content\")) > 0");
                    table.CheckConstraint("CK_PostComments_SenderDisplayName_NotBlank", "length(trim(\"SenderDisplayName\")) > 0");
                    table.ForeignKey(
                        name: "FK_PostComments_AiAccounts_AiAccountId",
                        column: x => x.AiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PostComments_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostImages_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostLikes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AiAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostLikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostLikes_AiAccounts_AiAccountId",
                        column: x => x.AiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PostLikes_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostComments_AiAccountId",
                table: "PostComments",
                column: "AiAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PostComments_PostId_CreatedAt_Id",
                table: "PostComments",
                columns: new[] { "PostId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PostImages_PostId_DisplayOrder",
                table: "PostImages",
                columns: new[] { "PostId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostLikes_AiAccountId",
                table: "PostLikes",
                column: "AiAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PostLikes_PostId",
                table: "PostLikes",
                column: "PostId",
                unique: true,
                filter: "\"AiAccountId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PostLikes_PostId_AiAccountId",
                table: "PostLikes",
                columns: new[] { "PostId", "AiAccountId" },
                unique: true,
                filter: "\"AiAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_AuthorAiAccountId",
                table: "Posts",
                column: "AuthorAiAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CreatedAt_Id",
                table: "Posts",
                columns: new[] { "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PostComments");

            migrationBuilder.DropTable(
                name: "PostImages");

            migrationBuilder.DropTable(
                name: "PostLikes");

            migrationBuilder.DropTable(
                name: "Posts");
        }
    }
}
