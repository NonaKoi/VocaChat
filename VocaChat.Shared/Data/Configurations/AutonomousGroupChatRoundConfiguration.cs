using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置自主好友群聊轮次、概率、计划规模和 Session 内唯一顺序。
/// </summary>
public sealed class AutonomousGroupChatRoundConfiguration
    : IEntityTypeConfiguration<AutonomousGroupChatRound>
{
    public void Configure(EntityTypeBuilder<AutonomousGroupChatRound> builder)
    {
        builder.ToTable("AutonomousGroupChatRounds", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousGroupChatRounds_RoundNumber",
                "\"RoundNumber\" > 0");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousGroupChatRounds_Probability",
                "\"OccurrenceProbability\" IS NULL "
                + "OR \"OccurrenceProbability\" BETWEEN 0 AND 1");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousGroupChatRounds_RandomRoll",
                "\"RandomRoll\" IS NULL OR \"RandomRoll\" BETWEEN 0 AND 1");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousGroupChatRounds_ClosingProbability",
                "(\"IsClosing\" = 1 AND \"OccurrenceProbability\" IS NULL "
                + "AND \"RandomRoll\" IS NULL) OR \"IsClosing\" = 0");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousGroupChatRounds_SpeakerCount",
                $"\"PlannedSpeakerCount\" BETWEEN 0 AND {AutonomousGroupChatRound.MaximumPlannedSpeakerCount}");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousGroupChatRounds_MessageCount",
                $"\"PlannedMessageCount\" BETWEEN 0 AND {AutonomousGroupChatRound.MaximumPlannedMessageCount}");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousGroupChatRounds_NormalRoundHasMessages",
                "\"IsClosing\" = 1 OR (\"PlannedSpeakerCount\" > 0 "
                + "AND \"PlannedMessageCount\" >= \"PlannedSpeakerCount\")");
        });

        builder.HasKey(round => round.Id);
        builder.Property(round => round.Id).ValueGeneratedNever();
        builder.Property(round => round.RoundNumber).IsRequired();
        builder.Property(round => round.IsClosing).IsRequired();
        builder.Property(round => round.PlannedSpeakerCount).IsRequired();
        builder.Property(round => round.PlannedMessageCount).IsRequired();
        builder.Property(round => round.StartedAt).IsRequired();

        builder.HasOne<AutonomousGroupChatSession>()
            .WithMany()
            .HasForeignKey(round => round.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(round => new { round.SessionId, round.RoundNumber })
            .IsUnique();
    }
}
