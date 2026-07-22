using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置群消息字段、发送者一致性、查询索引和外键关系。
/// </summary>
public sealed class GroupMessageConfiguration : IEntityTypeConfiguration<GroupMessage>
{
    public void Configure(EntityTypeBuilder<GroupMessage> builder)
    {
        builder.ToTable("GroupMessages", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_GroupMessages_SenderDisplayName_NotBlank",
                "length(trim(\"SenderDisplayName\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_GroupMessages_SenderDisplayName_MaxLength",
                $"length(\"SenderDisplayName\") <= {GroupMessage.SenderDisplayNameMaxLength}");
            tableBuilder.HasCheckConstraint(
                "CK_GroupMessages_Content_NotBlank",
                "length(trim(\"Content\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_GroupMessages_Content_MaxLength",
                $"length(\"Content\") <= {GroupMessage.ContentMaxLength}");
            tableBuilder.HasCheckConstraint(
                "CK_GroupMessages_Sender_Consistency",
                $"(\"SenderType\" = {(int)MessageSenderType.User} "
                + "AND \"SenderAiAccountId\" IS NULL) "
                + $"OR (\"SenderType\" = {(int)MessageSenderType.AiAccount} "
                + "AND \"SenderAiAccountId\" IS NOT NULL)");
            tableBuilder.HasCheckConstraint(
                "CK_GroupMessages_AutonomousRound_Consistency",
                "\"AutonomousGroupChatRoundId\" IS NULL "
                + "OR \"AutonomousGroupChatSessionId\" IS NOT NULL");
            tableBuilder.HasCheckConstraint(
                "CK_GroupMessages_SequenceNumber_Positive",
                "\"SequenceNumber\" > 0");
            tableBuilder.HasCheckConstraint(
                "CK_GroupMessages_ReplyTarget_NotSelf",
                "\"ReplyToMessageId\" IS NULL OR \"ReplyToMessageId\" <> \"Id\"");
            tableBuilder.HasCheckConstraint(
                "CK_GroupMessages_ReplyTarget_RequiresBatch",
                "\"ReplyToMessageId\" IS NULL OR \"InteractionBatchId\" IS NOT NULL");
        });

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .ValueGeneratedNever();

        builder.Property(message => message.GroupChatId)
            .IsRequired();

        builder.Property(message => message.SenderType)
            .IsRequired();

        builder.Property(message => message.SenderDisplayName)
            .IsRequired()
            .HasMaxLength(GroupMessage.SenderDisplayNameMaxLength);

        builder.Property(message => message.Content)
            .IsRequired()
            .HasMaxLength(GroupMessage.ContentMaxLength);

        builder.Property(message => message.SentAt)
            .IsRequired();

        builder.Property(message => message.SequenceNumber)
            .IsRequired();

        builder.HasOne<GroupChat>()
            .WithMany()
            .HasForeignKey(message => message.GroupChatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AiAccount>()
            .WithMany()
            .HasForeignKey(message => message.SenderAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AutonomousGroupChatSession>()
            .WithMany()
            .HasForeignKey(message => message.AutonomousGroupChatSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AutonomousGroupChatRound>()
            .WithMany()
            .HasForeignKey(message => message.AutonomousGroupChatRoundId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<GroupMessage>()
            .WithMany()
            .HasForeignKey(message => message.ReplyToMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(message => new
        {
            message.GroupChatId,
            message.SequenceNumber
        }).IsUnique();
        builder.HasIndex(message => new
        {
            message.GroupChatId,
            message.SentAt
        });
        builder.HasIndex(message => message.AutonomousGroupChatSessionId);
        builder.HasIndex(message => message.AutonomousGroupChatRoundId);
        builder.HasIndex(message => new
        {
            message.GroupChatId,
            message.InteractionBatchId
        });
        builder.HasIndex(message => message.ReplyToMessageId);
    }
}
