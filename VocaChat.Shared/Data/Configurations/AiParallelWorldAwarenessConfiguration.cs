using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置账号级平行世界元认知、来源消息和唯一账号约束。
/// </summary>
public sealed class AiParallelWorldAwarenessConfiguration
    : IEntityTypeConfiguration<AiParallelWorldAwareness>
{
    public void Configure(
        EntityTypeBuilder<AiParallelWorldAwareness> builder)
    {
        builder.ToTable("AiParallelWorldAwareness", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AiParallelWorldAwareness_State",
                $"\"State\" BETWEEN "
                + $"{(int)AiParallelWorldAwarenessState.Unaware} AND "
                + $"{(int)AiParallelWorldAwarenessState.Accepted}");
            tableBuilder.HasCheckConstraint(
                "CK_AiParallelWorldAwareness_Source",
                "\"LastSourcePrivateMessageId\" IS NULL "
                + "OR \"LastSourceGroupMessageId\" IS NULL");
            tableBuilder.HasCheckConstraint(
                "CK_AiParallelWorldAwareness_Timestamps",
                $"(\"State\" = {(int)AiParallelWorldAwarenessState.Unaware} "
                + "AND \"FirstInformedAt\" IS NULL "
                + "AND \"AcceptedAt\" IS NULL) OR "
                + $"(\"State\" = {(int)AiParallelWorldAwarenessState.Informed} "
                + "AND \"FirstInformedAt\" IS NOT NULL "
                + "AND \"AcceptedAt\" IS NULL) OR "
                + $"(\"State\" = {(int)AiParallelWorldAwarenessState.Accepted} "
                + "AND \"FirstInformedAt\" IS NOT NULL "
                + "AND \"AcceptedAt\" IS NOT NULL)");
        });

        builder.HasKey(awareness => awareness.Id);
        builder.Property(awareness => awareness.Id).ValueGeneratedNever();
        builder.Property(awareness => awareness.State).IsRequired();
        builder.Property(awareness => awareness.IsUserLocked).IsRequired();
        builder.Property(awareness => awareness.CreatedAt).IsRequired();
        builder.Property(awareness => awareness.UpdatedAt).IsRequired();

        builder.HasIndex(awareness => awareness.AiAccountId).IsUnique();
        builder.HasIndex(awareness => awareness.LastSourcePrivateMessageId);
        builder.HasIndex(awareness => awareness.LastSourceGroupMessageId);

        builder.HasOne(awareness => awareness.AiAccount)
            .WithMany()
            .HasForeignKey(awareness => awareness.AiAccountId)
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
