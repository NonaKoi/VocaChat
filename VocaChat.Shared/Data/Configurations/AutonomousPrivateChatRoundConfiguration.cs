using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置自主私信轮次的概率、发言形式、会话关系和唯一顺序。
/// </summary>
public sealed class AutonomousPrivateChatRoundConfiguration
    : IEntityTypeConfiguration<AutonomousPrivateChatRound>
{
    public void Configure(EntityTypeBuilder<AutonomousPrivateChatRound> builder)
    {
        builder.ToTable("AutonomousPrivateChatRounds", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousPrivateChatRounds_RoundNumber",
                "\"RoundNumber\" > 0");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousPrivateChatRounds_Probability",
                "\"OccurrenceProbability\" IS NULL "
                + "OR \"OccurrenceProbability\" BETWEEN 0 AND 1");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousPrivateChatRounds_RandomRoll",
                "\"RandomRoll\" IS NULL OR \"RandomRoll\" BETWEEN 0 AND 1");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousPrivateChatRounds_ClosingProbability",
                "(\"IsClosing\" = 1 AND \"OccurrenceProbability\" IS NULL "
                + "AND \"RandomRoll\" IS NULL) OR \"IsClosing\" = 0");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousPrivateChatRounds_InitiatorSpeaksInNormalRound",
                $"\"IsClosing\" = 1 OR \"InitiatorMessageMode\" <> "
                + $"{(int)AutonomousPrivateChatMessageMode.None}");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousPrivateChatRounds_MessageCounts",
                "\"InitiatorMessageCount\" BETWEEN 0 AND 3 "
                + "AND \"RecipientMessageCount\" BETWEEN 0 AND 3");
        });

        builder.HasKey(round => round.Id);
        builder.Property(round => round.Id).ValueGeneratedNever();
        builder.Property(round => round.RoundNumber).IsRequired();
        builder.Property(round => round.IsClosing).IsRequired();
        builder.Property(round => round.InitiatorMessageMode).IsRequired();
        builder.Property(round => round.RecipientMessageMode).IsRequired();
        builder.Property(round => round.InitiatorMessageCount).IsRequired();
        builder.Property(round => round.RecipientMessageCount).IsRequired();
        builder.Property(round => round.StartedAt).IsRequired();

        builder.HasOne<AutonomousPrivateChatSession>()
            .WithMany()
            .HasForeignKey(round => round.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(round => new { round.SessionId, round.RoundNumber })
            .IsUnique();
    }
}
