using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置当前本地用户与好友之间唯一的私聊关系。
/// </summary>
public sealed class PrivateChatConfiguration
    : IEntityTypeConfiguration<PrivateChat>
{
    public void Configure(EntityTypeBuilder<PrivateChat> builder)
    {
        builder.ToTable("PrivateChats", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_PrivateChats_Participants_Consistency",
                $"(\"Kind\" = {(int)PrivateChatKind.LocalUserAndAiAccount} "
                + "AND \"ContactId\" IS NOT NULL "
                + "AND \"FirstAiAccountId\" IS NULL "
                + "AND \"SecondAiAccountId\" IS NULL) OR "
                + $"(\"Kind\" = {(int)PrivateChatKind.AiAccounts} "
                + "AND \"ContactId\" IS NULL "
                + "AND \"FirstAiAccountId\" IS NOT NULL "
                + "AND \"SecondAiAccountId\" IS NOT NULL "
                + "AND \"FirstAiAccountId\" <> \"SecondAiAccountId\")");
        });
        builder.HasKey(chat => chat.Id);
        builder.Property(chat => chat.Id).ValueGeneratedNever();
        builder.Property(chat => chat.Kind).IsRequired();
        builder.Property(chat => chat.CreatedAt).IsRequired();

        builder.HasOne(chat => chat.Contact)
            .WithOne()
            .HasForeignKey<PrivateChat>(chat => chat.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(chat => chat.FirstAiAccount)
            .WithMany()
            .HasForeignKey(chat => chat.FirstAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(chat => chat.SecondAiAccount)
            .WithMany()
            .HasForeignKey(chat => chat.SecondAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(chat => chat.ContactId)
            .IsUnique()
            .HasFilter("\"ContactId\" IS NOT NULL");

        builder.HasIndex(chat => new
            {
                chat.FirstAiAccountId,
                chat.SecondAiAccountId
            })
            .IsUnique()
            .HasFilter(
                "\"FirstAiAccountId\" IS NOT NULL "
                + "AND \"SecondAiAccountId\" IS NOT NULL");
    }
}
