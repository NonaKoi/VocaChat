using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置本地用户唯一一份好友自主互动设置。
/// </summary>
public sealed class AutonomousInteractionSettingsConfiguration
    : IEntityTypeConfiguration<AutonomousInteractionSettings>
{
    public void Configure(
        EntityTypeBuilder<AutonomousInteractionSettings> builder)
    {
        builder.ToTable("AutonomousInteractionSettings", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousInteractionSettings_Singleton",
                $"\"Id\" = {AutonomousInteractionSettings.SingletonId}");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousInteractionSettings_Frequency",
                "\"Frequency\" IN ('Low', 'Normal', 'High')");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousInteractionSettings_PrivateChatContinuationRate",
                $"\"PrivateChatContinuationRatePercent\" BETWEEN "
                + $"{AutonomousInteractionSettings.MinimumPrivateChatContinuationRatePercent} "
                + $"AND {AutonomousInteractionSettings.MaximumPrivateChatContinuationRatePercent}");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousInteractionSettings_PrivateChatMaximumRounds",
                $"\"PrivateChatMaximumRounds\" BETWEEN "
                + $"{AutonomousInteractionSettings.MinimumPrivateChatMaximumRounds} "
                + $"AND {AutonomousInteractionSettings.MaximumPrivateChatMaximumRounds}");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousInteractionSettings_GroupChatMaximumMembers",
                $"\"AutonomousGroupChatMaximumMembers\" >= "
                + AutonomousInteractionSettings
                    .MinimumAutonomousGroupChatMaximumMembers);
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousInteractionSettings_GroupChatContinuationRate",
                $"\"GroupChatContinuationRatePercent\" BETWEEN "
                + $"{AutonomousInteractionSettings.MinimumGroupChatContinuationRatePercent} "
                + $"AND {AutonomousInteractionSettings.MaximumGroupChatContinuationRatePercent}");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousInteractionSettings_GroupChatMaximumRounds",
                $"\"GroupChatMaximumRounds\" BETWEEN "
                + $"{AutonomousInteractionSettings.MinimumGroupChatMaximumRounds} "
                + $"AND {AutonomousInteractionSettings.MaximumGroupChatMaximumRounds}");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousInteractionSettings_ReplyDelayMode",
                "\"ReplyDelayMode\" IN ('Fixed', 'RandomRange')");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousInteractionSettings_ReplyDelayValues",
                "\"FixedReplyDelayMilliseconds\" >= 0 "
                + "AND \"MinimumReplyDelayMilliseconds\" >= 0 "
                + "AND \"MaximumReplyDelayMilliseconds\" >= 0 "
                + "AND \"MinimumReplyDelayMilliseconds\" "
                + "<= \"MaximumReplyDelayMilliseconds\"");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousInteractionSettings_ConsecutiveMessageDelayMode",
                "\"ConsecutiveMessageDelayMode\" IN ('Fixed', 'RandomRange')");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousInteractionSettings_ConsecutiveMessageDelayValues",
                "\"FixedConsecutiveMessageDelayMilliseconds\" >= 0 "
                + "AND \"MinimumConsecutiveMessageDelayMilliseconds\" >= 0 "
                + "AND \"MaximumConsecutiveMessageDelayMilliseconds\" >= 0 "
                + "AND \"MinimumConsecutiveMessageDelayMilliseconds\" "
                + "<= \"MaximumConsecutiveMessageDelayMilliseconds\"");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousInteractionSettings_MaximumConsecutiveQuestionTurns",
                $"\"MaximumConsecutiveQuestionTurns\" >= "
                + AutonomousInteractionSettings
                    .MinimumMaximumConsecutiveQuestionTurns);
        });

        builder.HasKey(settings => settings.Id);

        builder.Property(settings => settings.Id)
            .ValueGeneratedNever();

        builder.Property(settings => settings.IsEnabled)
            .IsRequired();

        builder.Property(settings => settings.Frequency)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(settings => settings.AllowPrivateChats)
            .IsRequired();

        builder.Property(settings => settings.AllowGroupChats)
            .IsRequired();

        builder.Property(settings => settings.PrivateChatContinuationRatePercent)
            .HasDefaultValue(
                AutonomousInteractionSettings.DefaultPrivateChatContinuationRatePercent)
            .HasSentinel(-1)
            .IsRequired();

        builder.Property(settings => settings.PrivateChatMaximumRounds)
            .HasDefaultValue(
                AutonomousInteractionSettings.DefaultPrivateChatMaximumRounds)
            .IsRequired();

        builder.Property(settings => settings.AutonomousGroupChatMaximumMembers)
            .HasDefaultValue(
                AutonomousInteractionSettings
                    .DefaultAutonomousGroupChatMaximumMembers)
            .IsRequired();

        builder.Property(settings => settings.GroupChatContinuationRatePercent)
            .HasDefaultValue(
                AutonomousInteractionSettings
                    .DefaultGroupChatContinuationRatePercent)
            .HasSentinel(-1)
            .IsRequired();

        builder.Property(settings => settings.GroupChatMaximumRounds)
            .HasDefaultValue(
                AutonomousInteractionSettings.DefaultGroupChatMaximumRounds)
            .IsRequired();

        builder.Property(settings => settings.ReplyDelayMode)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(AutonomousInteractionSettings.DefaultReplyDelayMode)
            .HasSentinel((AiReplyDelayMode)(-1))
            .IsRequired();

        builder.Property(settings => settings.FixedReplyDelayMilliseconds)
            .HasDefaultValue(
                AutonomousInteractionSettings.DefaultFixedReplyDelayMilliseconds)
            .HasSentinel(-1L)
            .IsRequired();

        builder.Property(settings => settings.MinimumReplyDelayMilliseconds)
            .HasDefaultValue(
                AutonomousInteractionSettings.DefaultMinimumReplyDelayMilliseconds)
            .HasSentinel(-1L)
            .IsRequired();

        builder.Property(settings => settings.MaximumReplyDelayMilliseconds)
            .HasDefaultValue(
                AutonomousInteractionSettings.DefaultMaximumReplyDelayMilliseconds)
            .HasSentinel(-1L)
            .IsRequired();

        builder.Property(settings => settings.ConsecutiveMessageDelayMode)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(
                AutonomousInteractionSettings.DefaultConsecutiveMessageDelayMode)
            .HasSentinel((AiReplyDelayMode)(-1))
            .IsRequired();

        builder.Property(settings =>
                settings.FixedConsecutiveMessageDelayMilliseconds)
            .HasDefaultValue(AutonomousInteractionSettings
                .DefaultFixedConsecutiveMessageDelayMilliseconds)
            .HasSentinel(-1L)
            .IsRequired();

        builder.Property(settings =>
                settings.MinimumConsecutiveMessageDelayMilliseconds)
            .HasDefaultValue(AutonomousInteractionSettings
                .DefaultMinimumConsecutiveMessageDelayMilliseconds)
            .HasSentinel(-1L)
            .IsRequired();

        builder.Property(settings =>
                settings.MaximumConsecutiveMessageDelayMilliseconds)
            .HasDefaultValue(AutonomousInteractionSettings
                .DefaultMaximumConsecutiveMessageDelayMilliseconds)
            .HasSentinel(-1L)
            .IsRequired();

        builder.Property(settings => settings.MaximumConsecutiveQuestionTurns)
            .HasDefaultValue(AutonomousInteractionSettings
                .DefaultMaximumConsecutiveQuestionTurns)
            .IsRequired();
    }
}
