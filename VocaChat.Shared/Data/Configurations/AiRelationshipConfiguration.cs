using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置 AI 账号之间有方向关系的复合主键、数值约束和双外键。
/// </summary>
public sealed class AiRelationshipConfiguration
    : IEntityTypeConfiguration<AiRelationship>
{
    public void Configure(EntityTypeBuilder<AiRelationship> builder)
    {
        builder.ToTable("AiRelationships", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AiRelationships_DifferentAccounts",
                "\"FromAiAccountId\" <> \"ToAiAccountId\"");
            tableBuilder.HasCheckConstraint(
                "CK_AiRelationships_Familiarity",
                "\"Familiarity\" BETWEEN 0 AND 100");
            tableBuilder.HasCheckConstraint(
                "CK_AiRelationships_Affinity",
                "\"Affinity\" BETWEEN -100 AND 100");
            tableBuilder.HasCheckConstraint(
                "CK_AiRelationships_Trust",
                "\"Trust\" BETWEEN 0 AND 100");
            tableBuilder.HasCheckConstraint(
                "CK_AiRelationships_InteractionCount",
                "\"InteractionCount\" >= 0");
        });

        builder.HasKey(relationship => new
        {
            relationship.FromAiAccountId,
            relationship.ToAiAccountId
        });

        builder.Property(relationship => relationship.FromAiAccountId)
            .ValueGeneratedNever();
        builder.Property(relationship => relationship.ToAiAccountId)
            .ValueGeneratedNever();
        builder.Property(relationship => relationship.Familiarity)
            .IsRequired();
        builder.Property(relationship => relationship.Affinity)
            .IsRequired();
        builder.Property(relationship => relationship.Trust)
            .IsRequired();
        builder.Property(relationship => relationship.InteractionCount)
            .IsRequired();

        builder.HasIndex(relationship => relationship.ToAiAccountId);

        builder.HasOne(relationship => relationship.FromAiAccount)
            .WithMany()
            .HasForeignKey(relationship => relationship.FromAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(relationship => relationship.ToAiAccount)
            .WithMany()
            .HasForeignKey(relationship => relationship.ToAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
