using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddScopedSelfMemoryFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiSelfMemories_AiAccountId_Status_Salience_UpdatedAt",
                table: "AiSelfMemories");

            migrationBuilder.DropIndex(
                name: "IX_AiSelfMemories_AiAccountId_Type_Summary",
                table: "AiSelfMemories");

            migrationBuilder.AddColumn<Guid>(
                name: "CharacterWorldId",
                table: "AiSelfMemories",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "FactKey",
                table: "AiSelfMemories",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                collation: "NOCASE");

            migrationBuilder.AddColumn<int>(
                name: "FactNature",
                table: "AiSelfMemories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Mutability",
                table: "AiSelfMemories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesMemoryId",
                table: "AiSelfMemories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrustLevel",
                table: "AiSelfMemories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // 旧记忆先继承所属账号的角色世界，再建立非空外键。
            migrationBuilder.Sql(
                """
                UPDATE "AiSelfMemories"
                SET "CharacterWorldId" = (
                    SELECT "CharacterWorldId"
                    FROM "AiAccounts"
                    WHERE "AiAccounts"."Id" = "AiSelfMemories"."AiAccountId"
                );
                """);

            // 旧数据没有结构化事实键，使用记录 Id 生成稳定且互不冲突的迁移键。
            // 后续用户修订时可以显式整理为具备业务含义的事实键。
            migrationBuilder.Sql(
                """
                UPDATE "AiSelfMemories"
                SET "FactKey" = 'legacy.' || lower(replace("Id", '-', ''));
                """);

            // 按原有记忆类型补齐最保守的事实性质和可变性。
            // Type: 0 PersonalFact, 1 OngoingActivity, 2 Plan,
            //       3 Experience, 4 Preference.
            migrationBuilder.Sql(
                """
                UPDATE "AiSelfMemories"
                SET "FactNature" = CASE
                        WHEN "Type" = 4 THEN 1
                        WHEN "Type" = 3 THEN 2
                        ELSE 0
                    END,
                    "Mutability" = CASE
                        WHEN "Type" IN (0, 3) THEN 0
                        WHEN "Type" = 4 THEN 2
                        ELSE 1
                    END;
                """);

            // 用户来源记忆保持 UserCanon；导演偏好是 SubjectiveState，
            // 其他旧导演记忆先作为 NarrativeCandidate，不擅自升级可信等级。
            migrationBuilder.Sql(
                """
                UPDATE "AiSelfMemories"
                SET "TrustLevel" = CASE
                    WHEN "Source" = 0 THEN 0
                    WHEN "Type" = 4 THEN 3
                    ELSE 2
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AiSelfMemories_AiAccountId_CharacterWorldId_FactKey",
                table: "AiSelfMemories",
                columns: new[] { "AiAccountId", "CharacterWorldId", "FactKey" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AiSelfMemories_AiAccountId_CharacterWorldId_Status_Salience_UpdatedAt",
                table: "AiSelfMemories",
                columns: new[] { "AiAccountId", "CharacterWorldId", "Status", "Salience", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiSelfMemories_AiAccountId_CharacterWorldId_Type_Summary",
                table: "AiSelfMemories",
                columns: new[] { "AiAccountId", "CharacterWorldId", "Type", "Summary" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AiSelfMemories_CharacterWorldId",
                table: "AiSelfMemories",
                column: "CharacterWorldId");

            migrationBuilder.CreateIndex(
                name: "IX_AiSelfMemories_SupersedesMemoryId",
                table: "AiSelfMemories",
                column: "SupersedesMemoryId",
                unique: true,
                filter: "\"SupersedesMemoryId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiSelfMemories_FactKey",
                table: "AiSelfMemories",
                sql: "length(trim(\"FactKey\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiSelfMemories_FactKey_MaxLength",
                table: "AiSelfMemories",
                sql: "length(\"FactKey\") <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiSelfMemories_FactNature",
                table: "AiSelfMemories",
                sql: "\"FactNature\" BETWEEN 0 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiSelfMemories_Mutability",
                table: "AiSelfMemories",
                sql: "\"Mutability\" BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiSelfMemories_TrustLevel",
                table: "AiSelfMemories",
                sql: "\"TrustLevel\" BETWEEN 0 AND 3");

            migrationBuilder.AddForeignKey(
                name: "FK_AiSelfMemories_AiSelfMemories_SupersedesMemoryId",
                table: "AiSelfMemories",
                column: "SupersedesMemoryId",
                principalTable: "AiSelfMemories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AiSelfMemories_CharacterWorlds_CharacterWorldId",
                table: "AiSelfMemories",
                column: "CharacterWorldId",
                principalTable: "CharacterWorlds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiSelfMemories_AiSelfMemories_SupersedesMemoryId",
                table: "AiSelfMemories");

            migrationBuilder.DropForeignKey(
                name: "FK_AiSelfMemories_CharacterWorlds_CharacterWorldId",
                table: "AiSelfMemories");

            migrationBuilder.DropIndex(
                name: "IX_AiSelfMemories_AiAccountId_CharacterWorldId_FactKey",
                table: "AiSelfMemories");

            migrationBuilder.DropIndex(
                name: "IX_AiSelfMemories_AiAccountId_CharacterWorldId_Status_Salience_UpdatedAt",
                table: "AiSelfMemories");

            migrationBuilder.DropIndex(
                name: "IX_AiSelfMemories_AiAccountId_CharacterWorldId_Type_Summary",
                table: "AiSelfMemories");

            migrationBuilder.DropIndex(
                name: "IX_AiSelfMemories_CharacterWorldId",
                table: "AiSelfMemories");

            migrationBuilder.DropIndex(
                name: "IX_AiSelfMemories_SupersedesMemoryId",
                table: "AiSelfMemories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiSelfMemories_FactKey",
                table: "AiSelfMemories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiSelfMemories_FactKey_MaxLength",
                table: "AiSelfMemories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiSelfMemories_FactNature",
                table: "AiSelfMemories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiSelfMemories_Mutability",
                table: "AiSelfMemories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiSelfMemories_TrustLevel",
                table: "AiSelfMemories");

            migrationBuilder.DropColumn(
                name: "CharacterWorldId",
                table: "AiSelfMemories");

            migrationBuilder.DropColumn(
                name: "FactKey",
                table: "AiSelfMemories");

            migrationBuilder.DropColumn(
                name: "FactNature",
                table: "AiSelfMemories");

            migrationBuilder.DropColumn(
                name: "Mutability",
                table: "AiSelfMemories");

            migrationBuilder.DropColumn(
                name: "SupersedesMemoryId",
                table: "AiSelfMemories");

            migrationBuilder.DropColumn(
                name: "TrustLevel",
                table: "AiSelfMemories");

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
        }
    }
}
