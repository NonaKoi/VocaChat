using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置关系演化审计的方向唯一性、变化边界和来源外键。
/// </summary>
public sealed class AiRelationshipChangeConfiguration
    : IEntityTypeConfiguration<AiRelationshipChange>
{
    public void Configure(EntityTypeBuilder<AiRelationshipChange> builder)
    {
        builder.ToTable("AiRelationshipChanges", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AiRelationshipChanges_DifferentAccounts",
                "\"FromAiAccountId\" <> \"ToAiAccountId\"");
            tableBuilder.HasCheckConstraint(
                "CK_AiRelationshipChanges_FamiliarityDelta",
                $"\"FamiliarityDelta\" BETWEEN "
                + $"{AiRelationshipChange.MinimumFamiliarityDelta} AND "
                + $"{AiRelationshipChange.MaximumFamiliarityDelta}");
            tableBuilder.HasCheckConstraint(
                "CK_AiRelationshipChanges_AffinityDelta",
                $"\"AffinityDelta\" BETWEEN "
                + $"{AiRelationshipChange.MinimumAffinityDelta} AND "
                + $"{AiRelationshipChange.MaximumAffinityDelta}");
            tableBuilder.HasCheckConstraint(
                "CK_AiRelationshipChanges_TrustDelta",
                $"\"TrustDelta\" BETWEEN "
                + $"{AiRelationshipChange.MinimumTrustDelta} AND "
                + $"{AiRelationshipChange.MaximumTrustDelta}");
        });

        builder.HasKey(change => change.Id);
        builder.Property(change => change.Id).ValueGeneratedNever();
        builder.Property(change => change.FamiliarityDelta).IsRequired();
        builder.Property(change => change.AffinityDelta).IsRequired();
        builder.Property(change => change.TrustDelta).IsRequired();
        builder.Property(change => change.Reason)
            .IsRequired()
            .HasMaxLength(AiRelationshipChange.ReasonMaxLength);
        builder.Property(change => change.CreatedAt).IsRequired();

        builder.HasIndex(change => new
            {
                change.SessionId,
                change.FromAiAccountId,
                change.ToAiAccountId
            })
            .IsUnique();
        builder.HasIndex(change => new
        {
            change.FromAiAccountId,
            change.CreatedAt
        });
        builder.HasIndex(change => change.ToAiAccountId);

        builder.HasOne(change => change.Session)
            .WithMany()
            .HasForeignKey(change => change.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(change => change.FromAiAccount)
            .WithMany()
            .HasForeignKey(change => change.FromAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(change => change.ToAiAccount)
            .WithMany()
            .HasForeignKey(change => change.ToAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
