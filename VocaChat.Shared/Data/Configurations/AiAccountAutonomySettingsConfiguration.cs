using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置 AI 账号与其自主互动专有设置的一对一关系。
/// </summary>
public sealed class AiAccountAutonomySettingsConfiguration
    : IEntityTypeConfiguration<AiAccountAutonomySettings>
{
    public void Configure(EntityTypeBuilder<AiAccountAutonomySettings> builder)
    {
        builder.ToTable("AiAccountAutonomySettings", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AiAccountAutonomySettings_InitiativeLevel",
                "\"InitiativeLevel\" IN ('Low', 'Normal', 'High')");
            tableBuilder.HasCheckConstraint(
                "CK_AiAccountAutonomySettings_ReplyDelayMode",
                "\"ReplyDelayMode\" IN ('Fixed', 'RandomRange')");
            tableBuilder.HasCheckConstraint(
                "CK_AiAccountAutonomySettings_ReplyDelayValues",
                "\"FixedReplyDelayMilliseconds\" >= 0 "
                + "AND \"MinimumReplyDelayMilliseconds\" >= 0 "
                + "AND \"MaximumReplyDelayMilliseconds\" >= 0 "
                + "AND \"MinimumReplyDelayMilliseconds\" "
                + "<= \"MaximumReplyDelayMilliseconds\"");
            tableBuilder.HasCheckConstraint(
                "CK_AiAccountAutonomySettings_ConsecutiveMessageDelayMode",
                "\"ConsecutiveMessageDelayMode\" IN ('Fixed', 'RandomRange')");
            tableBuilder.HasCheckConstraint(
                "CK_AiAccountAutonomySettings_ConsecutiveMessageDelayValues",
                "\"FixedConsecutiveMessageDelayMilliseconds\" >= 0 "
                + "AND \"MinimumConsecutiveMessageDelayMilliseconds\" >= 0 "
                + "AND \"MaximumConsecutiveMessageDelayMilliseconds\" >= 0 "
                + "AND \"MinimumConsecutiveMessageDelayMilliseconds\" "
                + "<= \"MaximumConsecutiveMessageDelayMilliseconds\"");
            tableBuilder.HasCheckConstraint(
                "CK_AiAccountAutonomySettings_MaximumConsecutiveQuestionTurns",
                $"\"MaximumConsecutiveQuestionTurns\" >= "
                + AutonomousInteractionSettings
                    .MinimumMaximumConsecutiveQuestionTurns);
        });

        builder.HasKey(settings => settings.AiAccountId);

        builder.Property(settings => settings.AiAccountId)
            .ValueGeneratedNever();

        builder.Property(settings => settings.IsEnabled)
            .IsRequired();

        builder.Property(settings => settings.InitiativeLevel)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(settings => settings.CanInitiatePrivateChats)
            .IsRequired();

        builder.Property(settings => settings.CanInitiateGroupChats)
            .IsRequired();

        builder.Property(settings => settings.CanJoinGroupChats)
            .IsRequired();

        builder.Property(settings => settings.UseGlobalReplyDelay)
            .HasDefaultValue(true)
            .HasSentinel(true)
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

        builder.Property(settings => settings.UseGlobalConsecutiveMessageDelay)
            .HasDefaultValue(true)
            .HasSentinel(true)
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

        builder.Property(settings => settings.UseGlobalQuestionPolicy)
            .HasDefaultValue(true)
            .HasSentinel(true)
            .IsRequired();

        builder.Property(settings => settings.MaximumConsecutiveQuestionTurns)
            .HasDefaultValue(AutonomousInteractionSettings
                .DefaultMaximumConsecutiveQuestionTurns)
            .IsRequired();

        builder.HasOne<AiAccount>()
            .WithOne()
            .HasForeignKey<AiAccountAutonomySettings>(settings =>
                settings.AiAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
