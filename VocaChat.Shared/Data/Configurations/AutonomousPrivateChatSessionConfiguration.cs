using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置好友自主私信 Session 的生命周期约束、外键和查询索引。
/// </summary>
public sealed class AutonomousPrivateChatSessionConfiguration
    : IEntityTypeConfiguration<AutonomousPrivateChatSession>
{
    public void Configure(
        EntityTypeBuilder<AutonomousPrivateChatSession> builder)
    {
        builder.ToTable("AutonomousPrivateChatSessions", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousPrivateChatSessions_DifferentParticipants",
                "\"InitiatorAiAccountId\" <> \"RecipientAiAccountId\"");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousPrivateChatSessions_RoundLimits",
                $"\"MaximumRounds\" BETWEEN "
                + $"{AutonomousInteractionSettings.MinimumPrivateChatMaximumRounds} "
                + $"AND {AutonomousInteractionSettings.MaximumPrivateChatMaximumRounds} "
                + "AND \"CompletedRounds\" BETWEEN 0 AND \"MaximumRounds\"");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousPrivateChatSessions_ContinuationRate",
                $"\"ContinuationRatePercent\" BETWEEN "
                + $"{AutonomousInteractionSettings.MinimumPrivateChatContinuationRatePercent} "
                + $"AND {AutonomousInteractionSettings.MaximumPrivateChatContinuationRatePercent}");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousPrivateChatSessions_StateConsistency",
                $"(\"Status\" = {(int)AutonomousPrivateChatSessionStatus.Running} "
                + "AND \"EndReason\" IS NULL AND \"EndedAt\" IS NULL) OR "
                + $"(\"Status\" IN ({(int)AutonomousPrivateChatSessionStatus.Completed}, "
                + $"{(int)AutonomousPrivateChatSessionStatus.Failed}, "
                + $"{(int)AutonomousPrivateChatSessionStatus.Cancelled}) "
                + "AND \"EndReason\" IS NOT NULL AND \"EndedAt\" IS NOT NULL)");
        });

        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.Topic)
            .IsRequired()
            .HasMaxLength(AutonomousPrivateChatSession.TopicMaxLength);
        builder.Property(session => session.MaximumRounds).IsRequired();
        builder.Property(session => session.ContinuationRatePercent)
            .HasDefaultValue(
                AutonomousInteractionSettings.DefaultPrivateChatContinuationRatePercent)
            .HasSentinel(-1)
            .IsRequired();
        builder.Property(session => session.CompletedRounds).IsRequired();
        builder.Property(session => session.Status).IsRequired();
        builder.Property(session => session.StartedAt).IsRequired();
        builder.Property(session => session.LastActivityAt).IsRequired();

        builder.HasOne<PrivateChat>()
            .WithMany()
            .HasForeignKey(session => session.PrivateChatId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AiAccount>()
            .WithMany()
            .HasForeignKey(session => session.InitiatorAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AiAccount>()
            .WithMany()
            .HasForeignKey(session => session.RecipientAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(session => session.PrivateChatId)
            .IsUnique()
            .HasFilter(
                $"\"Status\" = {(int)AutonomousPrivateChatSessionStatus.Running}");
        builder.HasIndex(session => new
        {
            session.PrivateChatId,
            session.StartedAt
        });
        builder.HasIndex(session => new
        {
            session.Status,
            session.LastActivityAt
        });
    }
}
