using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置私聊消息、发送者一致性、查询索引和外键。
/// </summary>
public sealed class PrivateMessageConfiguration
    : IEntityTypeConfiguration<PrivateMessage>
{
    public void Configure(EntityTypeBuilder<PrivateMessage> builder)
    {
        builder.ToTable("PrivateMessages", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_PrivateMessages_SenderDisplayName_NotBlank",
                "length(trim(\"SenderDisplayName\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_PrivateMessages_Content_NotBlank",
                "length(trim(\"Content\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_PrivateMessages_Sender_Consistency",
                $"(\"SenderType\" = {(int)MessageSenderType.User} AND \"SenderAiAccountId\" IS NULL) "
                + $"OR (\"SenderType\" = {(int)MessageSenderType.AiAccount} AND \"SenderAiAccountId\" IS NOT NULL)");
        });

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.SenderType).IsRequired();
        builder.Property(message => message.SenderDisplayName)
            .IsRequired()
            .HasMaxLength(PrivateMessage.SenderDisplayNameMaxLength);
        builder.Property(message => message.Content)
            .IsRequired()
            .HasMaxLength(PrivateMessage.ContentMaxLength);
        builder.Property(message => message.SentAt).IsRequired();

        builder.HasOne<PrivateChat>()
            .WithMany()
            .HasForeignKey(message => message.PrivateChatId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AiAccount>()
            .WithMany()
            .HasForeignKey(message => message.SenderAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(message => new
        {
            message.PrivateChatId,
            message.SentAt,
            message.Id
        });
    }
}
