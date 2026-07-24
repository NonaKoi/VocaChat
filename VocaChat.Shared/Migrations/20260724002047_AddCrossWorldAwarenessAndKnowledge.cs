using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddCrossWorldAwarenessAndKnowledge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiParallelWorldAwareness",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstInformedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSourcePrivateMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastSourceGroupMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsUserLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiParallelWorldAwareness", x => x.Id);
                    table.CheckConstraint("CK_AiParallelWorldAwareness_Source", "\"LastSourcePrivateMessageId\" IS NULL OR \"LastSourceGroupMessageId\" IS NULL");
                    table.CheckConstraint("CK_AiParallelWorldAwareness_State", "\"State\" BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_AiParallelWorldAwareness_Timestamps", "(\"State\" = 0 AND \"FirstInformedAt\" IS NULL AND \"AcceptedAt\" IS NULL) OR (\"State\" = 1 AND \"FirstInformedAt\" IS NOT NULL AND \"AcceptedAt\" IS NULL) OR (\"State\" = 2 AND \"FirstInformedAt\" IS NOT NULL AND \"AcceptedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AiParallelWorldAwareness_AiAccounts_AiAccountId",
                        column: x => x.AiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiParallelWorldAwareness_GroupMessages_LastSourceGroupMessageId",
                        column: x => x.LastSourceGroupMessageId,
                        principalTable: "GroupMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiParallelWorldAwareness_PrivateMessages_LastSourcePrivateMessageId",
                        column: x => x.LastSourcePrivateMessageId,
                        principalTable: "PrivateMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiWorldAwareness",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ObserverAiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectAiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectCharacterWorldId = table.Column<Guid>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    EvidenceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DistinctConversationCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstEvidenceAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastEvidenceAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSourcePrivateMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastSourceGroupMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsUserLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiWorldAwareness", x => x.Id);
                    table.CheckConstraint("CK_AiWorldAwareness_ConfirmedAt", "\"ConfirmedAt\" IS NULL OR \"State\" = 3");
                    table.CheckConstraint("CK_AiWorldAwareness_ConversationCount", "\"DistinctConversationCount\" >= 0");
                    table.CheckConstraint("CK_AiWorldAwareness_DifferentAccounts", "\"ObserverAiAccountId\" <> \"SubjectAiAccountId\"");
                    table.CheckConstraint("CK_AiWorldAwareness_EvidenceCount", "\"EvidenceCount\" >= 0");
                    table.CheckConstraint("CK_AiWorldAwareness_EvidenceTimes", "(\"FirstEvidenceAt\" IS NULL AND \"LastEvidenceAt\" IS NULL) OR (\"FirstEvidenceAt\" IS NOT NULL AND \"LastEvidenceAt\" IS NOT NULL AND \"LastEvidenceAt\" >= \"FirstEvidenceAt\")");
                    table.CheckConstraint("CK_AiWorldAwareness_Source", "\"LastSourcePrivateMessageId\" IS NULL OR \"LastSourceGroupMessageId\" IS NULL");
                    table.CheckConstraint("CK_AiWorldAwareness_State", "\"State\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_AiWorldAwareness_AiAccounts_ObserverAiAccountId",
                        column: x => x.ObserverAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiWorldAwareness_AiAccounts_SubjectAiAccountId",
                        column: x => x.SubjectAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiWorldAwareness_CharacterWorlds_SubjectCharacterWorldId",
                        column: x => x.SubjectCharacterWorldId,
                        principalTable: "CharacterWorlds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiWorldAwareness_GroupMessages_LastSourceGroupMessageId",
                        column: x => x.LastSourceGroupMessageId,
                        principalTable: "GroupMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiWorldAwareness_PrivateMessages_LastSourcePrivateMessageId",
                        column: x => x.LastSourcePrivateMessageId,
                        principalTable: "PrivateMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiWorldKnowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerAiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectCharacterWorldId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectAiAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    KnowledgeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false, collation: "NOCASE"),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FactNature = table.Column<int>(type: "INTEGER", nullable: false),
                    Mutability = table.Column<int>(type: "INTEGER", nullable: false),
                    TrustLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Salience = table.Column<int>(type: "INTEGER", nullable: false),
                    IsUserLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstLearnedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiWorldKnowledge", x => x.Id);
                    table.CheckConstraint("CK_AiWorldKnowledge_FactNature", "\"FactNature\" BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_AiWorldKnowledge_KnowledgeKey_NotBlank", "length(trim(\"KnowledgeKey\")) > 0");
                    table.CheckConstraint("CK_AiWorldKnowledge_Mutability", "\"Mutability\" BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_AiWorldKnowledge_Salience", "\"Salience\" BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_AiWorldKnowledge_Status", "\"Status\" BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_AiWorldKnowledge_Summary_NotBlank", "length(trim(\"Summary\")) > 0");
                    table.CheckConstraint("CK_AiWorldKnowledge_TrustLevel", "\"TrustLevel\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_AiWorldKnowledge_AiAccounts_OwnerAiAccountId",
                        column: x => x.OwnerAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiWorldKnowledge_AiAccounts_SubjectAiAccountId",
                        column: x => x.SubjectAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiWorldKnowledge_CharacterWorlds_SubjectCharacterWorldId",
                        column: x => x.SubjectCharacterWorldId,
                        principalTable: "CharacterWorlds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GroupMessageAudience",
                columns: table => new
                {
                    GroupMessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AiAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VisibleAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMessageAudience", x => new { x.GroupMessageId, x.AiAccountId });
                    table.ForeignKey(
                        name: "FK_GroupMessageAudience_AiAccounts_AiAccountId",
                        column: x => x.AiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupMessageAudience_GroupMessages_GroupMessageId",
                        column: x => x.GroupMessageId,
                        principalTable: "GroupMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiWorldKnowledgeEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AiWorldKnowledgeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceAiAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourcePrivateMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceGroupMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EvidenceSummary = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ObservedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiWorldKnowledgeEvidence", x => x.Id);
                    table.CheckConstraint("CK_AiWorldKnowledgeEvidence_SourceMessage", "(\"SourcePrivateMessageId\" IS NOT NULL AND \"SourceGroupMessageId\" IS NULL) OR (\"SourcePrivateMessageId\" IS NULL AND \"SourceGroupMessageId\" IS NOT NULL)");
                    table.CheckConstraint("CK_AiWorldKnowledgeEvidence_SourceType", "(\"SourceType\" = 0 AND \"SourceAiAccountId\" IS NULL) OR (\"SourceType\" = 1 AND \"SourceAiAccountId\" IS NOT NULL)");
                    table.CheckConstraint("CK_AiWorldKnowledgeEvidence_Summary_NotBlank", "length(trim(\"EvidenceSummary\")) > 0");
                    table.ForeignKey(
                        name: "FK_AiWorldKnowledgeEvidence_AiAccounts_SourceAiAccountId",
                        column: x => x.SourceAiAccountId,
                        principalTable: "AiAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiWorldKnowledgeEvidence_AiWorldKnowledge_AiWorldKnowledgeId",
                        column: x => x.AiWorldKnowledgeId,
                        principalTable: "AiWorldKnowledge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiWorldKnowledgeEvidence_GroupMessages_SourceGroupMessageId",
                        column: x => x.SourceGroupMessageId,
                        principalTable: "GroupMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiWorldKnowledgeEvidence_PrivateMessages_SourcePrivateMessageId",
                        column: x => x.SourcePrivateMessageId,
                        principalTable: "PrivateMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiParallelWorldAwareness_AiAccountId",
                table: "AiParallelWorldAwareness",
                column: "AiAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiParallelWorldAwareness_LastSourceGroupMessageId",
                table: "AiParallelWorldAwareness",
                column: "LastSourceGroupMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_AiParallelWorldAwareness_LastSourcePrivateMessageId",
                table: "AiParallelWorldAwareness",
                column: "LastSourcePrivateMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorldAwareness_LastSourceGroupMessageId",
                table: "AiWorldAwareness",
                column: "LastSourceGroupMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorldAwareness_LastSourcePrivateMessageId",
                table: "AiWorldAwareness",
                column: "LastSourcePrivateMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorldAwareness_ObserverAiAccountId_SubjectAiAccountId",
                table: "AiWorldAwareness",
                columns: new[] { "ObserverAiAccountId", "SubjectAiAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiWorldAwareness_SubjectAiAccountId",
                table: "AiWorldAwareness",
                column: "SubjectAiAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorldAwareness_SubjectCharacterWorldId",
                table: "AiWorldAwareness",
                column: "SubjectCharacterWorldId");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorldKnowledge_OwnerAiAccountId_SubjectCharacterWorldId_KnowledgeKey",
                table: "AiWorldKnowledge",
                columns: new[] { "OwnerAiAccountId", "SubjectCharacterWorldId", "KnowledgeKey" },
                unique: true,
                filter: "\"Status\" = 0 AND \"SubjectAiAccountId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorldKnowledge_OwnerAiAccountId_SubjectCharacterWorldId_Status_Salience_UpdatedAt",
                table: "AiWorldKnowledge",
                columns: new[] { "OwnerAiAccountId", "SubjectCharacterWorldId", "Status", "Salience", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiWorldKnowledge_OwnerAiAccountId_SubjectCharacterWorldId_SubjectAiAccountId_KnowledgeKey",
                table: "AiWorldKnowledge",
                columns: new[] { "OwnerAiAccountId", "SubjectCharacterWorldId", "SubjectAiAccountId", "KnowledgeKey" },
                unique: true,
                filter: "\"Status\" = 0 AND \"SubjectAiAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorldKnowledge_SubjectAiAccountId",
                table: "AiWorldKnowledge",
                column: "SubjectAiAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorldKnowledge_SubjectCharacterWorldId",
                table: "AiWorldKnowledge",
                column: "SubjectCharacterWorldId");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorldKnowledgeEvidence_AiWorldKnowledgeId_SourceGroupMessageId",
                table: "AiWorldKnowledgeEvidence",
                columns: new[] { "AiWorldKnowledgeId", "SourceGroupMessageId" },
                unique: true,
                filter: "\"SourceGroupMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorldKnowledgeEvidence_AiWorldKnowledgeId_SourcePrivateMessageId",
                table: "AiWorldKnowledgeEvidence",
                columns: new[] { "AiWorldKnowledgeId", "SourcePrivateMessageId" },
                unique: true,
                filter: "\"SourcePrivateMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorldKnowledgeEvidence_SourceAiAccountId",
                table: "AiWorldKnowledgeEvidence",
                column: "SourceAiAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorldKnowledgeEvidence_SourceGroupMessageId",
                table: "AiWorldKnowledgeEvidence",
                column: "SourceGroupMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorldKnowledgeEvidence_SourcePrivateMessageId",
                table: "AiWorldKnowledgeEvidence",
                column: "SourcePrivateMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessageAudience_AiAccountId_VisibleAt",
                table: "GroupMessageAudience",
                columns: new[] { "AiAccountId", "VisibleAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiParallelWorldAwareness");

            migrationBuilder.DropTable(
                name: "AiWorldAwareness");

            migrationBuilder.DropTable(
                name: "AiWorldKnowledgeEvidence");

            migrationBuilder.DropTable(
                name: "GroupMessageAudience");

            migrationBuilder.DropTable(
                name: "AiWorldKnowledge");
        }
    }
}
