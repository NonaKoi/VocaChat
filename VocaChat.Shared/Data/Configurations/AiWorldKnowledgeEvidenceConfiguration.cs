using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置世界知识和正式私聊或群聊消息之间的来源证据。
/// </summary>
public sealed class AiWorldKnowledgeEvidenceConfiguration
    : IEntityTypeConfiguration<AiWorldKnowledgeEvidence>
{
    public void Configure(
        EntityTypeBuilder<AiWorldKnowledgeEvidence> builder)
    {
        builder.ToTable("AiWorldKnowledgeEvidence", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldKnowledgeEvidence_SourceMessage",
                "(\"SourcePrivateMessageId\" IS NOT NULL "
                + "AND \"SourceGroupMessageId\" IS NULL) OR "
                + "(\"SourcePrivateMessageId\" IS NULL "
                + "AND \"SourceGroupMessageId\" IS NOT NULL)");
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldKnowledgeEvidence_SourceType",
                $"(\"SourceType\" = {(int)MessageSenderType.User} "
                + "AND \"SourceAiAccountId\" IS NULL) OR "
                + $"(\"SourceType\" = {(int)MessageSenderType.AiAccount} "
                + "AND \"SourceAiAccountId\" IS NOT NULL)");
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldKnowledgeEvidence_Summary_NotBlank",
                "length(trim(\"EvidenceSummary\")) > 0");
        });

        builder.HasKey(evidence => evidence.Id);
        builder.Property(evidence => evidence.Id).ValueGeneratedNever();
        builder.Property(evidence => evidence.SourceType).IsRequired();
        builder.Property(evidence => evidence.EvidenceSummary)
            .IsRequired()
            .HasMaxLength(AiWorldKnowledgeEvidence.EvidenceSummaryMaxLength);
        builder.Property(evidence => evidence.ObservedAt).IsRequired();

        builder.HasIndex(evidence => new
        {
            evidence.AiWorldKnowledgeId,
            evidence.SourcePrivateMessageId
        })
            .IsUnique()
            .HasFilter("\"SourcePrivateMessageId\" IS NOT NULL");
        builder.HasIndex(evidence => new
        {
            evidence.AiWorldKnowledgeId,
            evidence.SourceGroupMessageId
        })
            .IsUnique()
            .HasFilter("\"SourceGroupMessageId\" IS NOT NULL");
        builder.HasIndex(evidence => evidence.SourceAiAccountId);

        builder.HasOne(evidence => evidence.AiWorldKnowledge)
            .WithMany()
            .HasForeignKey(evidence => evidence.AiWorldKnowledgeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(evidence => evidence.SourceAiAccount)
            .WithMany()
            .HasForeignKey(evidence => evidence.SourceAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PrivateMessage>()
            .WithMany()
            .HasForeignKey(evidence => evidence.SourcePrivateMessageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupMessage>()
            .WithMany()
            .HasForeignKey(evidence => evidence.SourceGroupMessageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
