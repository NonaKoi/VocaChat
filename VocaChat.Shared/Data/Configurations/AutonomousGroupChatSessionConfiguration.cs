using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置自主好友群聊 Session、参与者快照和生命周期约束。
/// </summary>
public sealed class AutonomousGroupChatSessionConfiguration
    : IEntityTypeConfiguration<AutonomousGroupChatSession>
{
    public void Configure(
        EntityTypeBuilder<AutonomousGroupChatSession> builder)
    {
        builder.ToTable("AutonomousGroupChatSessions", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousGroupChatSessions_StateConsistency",
                $"(\"Status\" = {(int)AutonomousGroupChatSessionStatus.Running} "
                + "AND \"EndReason\" IS NULL AND \"EndedAt\" IS NULL) OR "
                + $"(\"Status\" IN ({(int)AutonomousGroupChatSessionStatus.Completed}, "
                + $"{(int)AutonomousGroupChatSessionStatus.Failed}) "
                + "AND \"EndReason\" IS NOT NULL AND \"EndedAt\" IS NOT NULL)");
        });

        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.Topic)
            .IsRequired()
            .HasMaxLength(AutonomousGroupChatSession.TopicMaxLength);
        builder.Property(session => session.Status).IsRequired();
        builder.Property(session => session.StartedAt).IsRequired();
        builder.Property(session => session.LastActivityAt).IsRequired();

        builder.HasOne<GroupChat>()
            .WithMany()
            .HasForeignKey(session => session.GroupChatId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AiAccount>()
            .WithMany()
            .HasForeignKey(session => session.InitiatorAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(session => session.Participants)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "AutonomousGroupChatSessionParticipants",
                right => right
                    .HasOne<AiAccount>()
                    .WithMany()
                    .HasForeignKey("AiAccountId")
                    .OnDelete(DeleteBehavior.Restrict),
                left => left
                    .HasOne<AutonomousGroupChatSession>()
                    .WithMany()
                    .HasForeignKey("SessionId")
                    .OnDelete(DeleteBehavior.Restrict),
                join =>
                {
                    join.ToTable("AutonomousGroupChatSessionParticipants");
                    join.HasKey("SessionId", "AiAccountId");
                    join.HasIndex("AiAccountId");
                });

        builder.Navigation(session => session.Participants)
            .HasField("_participants")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(session => session.GroupChatId)
            .IsUnique()
            .HasFilter(
                $"\"Status\" = {(int)AutonomousGroupChatSessionStatus.Running}");
        builder.HasIndex(session => new
        {
            session.GroupChatId,
            session.StartedAt
        });
        builder.HasIndex(session => new
        {
            session.Status,
            session.LastActivityAt
        });
    }
}
