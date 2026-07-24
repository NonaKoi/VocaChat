using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置方向性世界知识的作用域、分类、显著度和有效记录唯一性。
/// </summary>
public sealed class AiWorldKnowledgeConfiguration
    : IEntityTypeConfiguration<AiWorldKnowledge>
{
    public void Configure(EntityTypeBuilder<AiWorldKnowledge> builder)
    {
        builder.ToTable("AiWorldKnowledge", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldKnowledge_KnowledgeKey_NotBlank",
                "length(trim(\"KnowledgeKey\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldKnowledge_Summary_NotBlank",
                "length(trim(\"Summary\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldKnowledge_FactNature",
                $"\"FactNature\" BETWEEN "
                + $"{(int)AiWorldKnowledgeFactNature.ObjectiveStatement} AND "
                + $"{(int)AiWorldKnowledgeFactNature.Unconfirmed}");
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldKnowledge_Mutability",
                $"\"Mutability\" BETWEEN "
                + $"{(int)AiWorldKnowledgeMutability.Constant} AND "
                + $"{(int)AiWorldKnowledgeMutability.Temporary}");
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldKnowledge_TrustLevel",
                $"\"TrustLevel\" BETWEEN "
                + $"{(int)AiWorldKnowledgeTrustLevel.Unverified} AND "
                + $"{(int)AiWorldKnowledgeTrustLevel.UserConfirmed}");
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldKnowledge_Status",
                $"\"Status\" BETWEEN "
                + $"{(int)AiWorldKnowledgeStatus.Active} AND "
                + $"{(int)AiWorldKnowledgeStatus.ConflictCandidate}");
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldKnowledge_Salience",
                $"\"Salience\" BETWEEN {AiWorldKnowledge.MinimumSalience} "
                + $"AND {AiWorldKnowledge.MaximumSalience}");
        });

        builder.HasKey(knowledge => knowledge.Id);
        builder.Property(knowledge => knowledge.Id).ValueGeneratedNever();
        builder.Property(knowledge => knowledge.KnowledgeKey)
            .IsRequired()
            .HasMaxLength(AiWorldKnowledge.KnowledgeKeyMaxLength)
            .UseCollation("NOCASE");
        builder.Property(knowledge => knowledge.Summary)
            .IsRequired()
            .HasMaxLength(AiWorldKnowledge.SummaryMaxLength);
        builder.Property(knowledge => knowledge.FactNature).IsRequired();
        builder.Property(knowledge => knowledge.Mutability).IsRequired();
        builder.Property(knowledge => knowledge.TrustLevel).IsRequired();
        builder.Property(knowledge => knowledge.Status).IsRequired();
        builder.Property(knowledge => knowledge.Salience).IsRequired();
        builder.Property(knowledge => knowledge.IsUserLocked).IsRequired();
        builder.Property(knowledge => knowledge.FirstLearnedAt).IsRequired();
        builder.Property(knowledge => knowledge.UpdatedAt).IsRequired();

        builder.HasIndex(knowledge => new
        {
            knowledge.OwnerAiAccountId,
            knowledge.SubjectCharacterWorldId,
            knowledge.SubjectAiAccountId,
            knowledge.KnowledgeKey
        })
            .IsUnique()
            .HasFilter(
                $"\"Status\" = {(int)AiWorldKnowledgeStatus.Active} "
                + "AND \"SubjectAiAccountId\" IS NOT NULL");
        builder.HasIndex(knowledge => new
        {
            knowledge.OwnerAiAccountId,
            knowledge.SubjectCharacterWorldId,
            knowledge.KnowledgeKey
        })
            .IsUnique()
            .HasFilter(
                $"\"Status\" = {(int)AiWorldKnowledgeStatus.Active} "
                + "AND \"SubjectAiAccountId\" IS NULL");
        builder.HasIndex(knowledge => new
        {
            knowledge.OwnerAiAccountId,
            knowledge.SubjectCharacterWorldId,
            knowledge.Status,
            knowledge.Salience,
            knowledge.UpdatedAt
        });
        builder.HasIndex(knowledge => knowledge.SubjectAiAccountId);

        builder.HasOne(knowledge => knowledge.OwnerAiAccount)
            .WithMany()
            .HasForeignKey(knowledge => knowledge.OwnerAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(knowledge => knowledge.SubjectCharacterWorld)
            .WithMany()
            .HasForeignKey(knowledge => knowledge.SubjectCharacterWorldId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(knowledge => knowledge.SubjectAiAccount)
            .WithMany()
            .HasForeignKey(knowledge => knowledge.SubjectAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
