using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置 AI 账号之间方向性世界认知、统计字段和来源消息。
/// </summary>
public sealed class AiWorldAwarenessConfiguration
    : IEntityTypeConfiguration<AiWorldAwareness>
{
    public void Configure(EntityTypeBuilder<AiWorldAwareness> builder)
    {
        builder.ToTable("AiWorldAwareness", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldAwareness_DifferentAccounts",
                "\"ObserverAiAccountId\" <> \"SubjectAiAccountId\"");
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldAwareness_State",
                $"\"State\" BETWEEN "
                + $"{(int)AiWorldAwarenessState.AssumedSharedWorld} AND "
                + $"{(int)AiWorldAwarenessState.CrossWorldConfirmed}");
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldAwareness_EvidenceCount",
                "\"EvidenceCount\" >= 0");
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldAwareness_ConversationCount",
                "\"DistinctConversationCount\" >= 0");
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldAwareness_EvidenceTimes",
                "(\"FirstEvidenceAt\" IS NULL "
                + "AND \"LastEvidenceAt\" IS NULL) OR "
                + "(\"FirstEvidenceAt\" IS NOT NULL "
                + "AND \"LastEvidenceAt\" IS NOT NULL "
                + "AND \"LastEvidenceAt\" >= \"FirstEvidenceAt\")");
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldAwareness_ConfirmedAt",
                "\"ConfirmedAt\" IS NULL "
                + $"OR \"State\" = {(int)AiWorldAwarenessState.CrossWorldConfirmed}");
            tableBuilder.HasCheckConstraint(
                "CK_AiWorldAwareness_Source",
                "\"LastSourcePrivateMessageId\" IS NULL "
                + "OR \"LastSourceGroupMessageId\" IS NULL");
        });

        builder.HasKey(awareness => awareness.Id);
        builder.Property(awareness => awareness.Id).ValueGeneratedNever();
        builder.Property(awareness => awareness.State).IsRequired();
        builder.Property(awareness => awareness.EvidenceCount).IsRequired();
        builder.Property(awareness => awareness.DistinctConversationCount)
            .IsRequired();
        builder.Property(awareness => awareness.IsUserLocked).IsRequired();
        builder.Property(awareness => awareness.CreatedAt).IsRequired();
        builder.Property(awareness => awareness.UpdatedAt).IsRequired();

        builder.HasIndex(awareness => new
        {
            awareness.ObserverAiAccountId,
            awareness.SubjectAiAccountId
        }).IsUnique();
        builder.HasIndex(awareness => awareness.SubjectCharacterWorldId);
        builder.HasIndex(awareness => awareness.LastSourcePrivateMessageId);
        builder.HasIndex(awareness => awareness.LastSourceGroupMessageId);

        builder.HasOne(awareness => awareness.ObserverAiAccount)
            .WithMany()
            .HasForeignKey(awareness => awareness.ObserverAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(awareness => awareness.SubjectAiAccount)
            .WithMany()
            .HasForeignKey(awareness => awareness.SubjectAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(awareness => awareness.SubjectCharacterWorld)
            .WithMany()
            .HasForeignKey(awareness => awareness.SubjectCharacterWorldId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PrivateMessage>()
            .WithMany()
            .HasForeignKey(awareness => awareness.LastSourcePrivateMessageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupMessage>()
            .WithMany()
            .HasForeignKey(awareness => awareness.LastSourceGroupMessageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
