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
        builder.ToTable("PrivateChats");
        builder.HasKey(chat => chat.Id);
        builder.Property(chat => chat.Id).ValueGeneratedNever();
        builder.Property(chat => chat.CreatedAt).IsRequired();

        builder.HasOne(chat => chat.Contact)
            .WithOne()
            .HasForeignKey<PrivateChat>(chat => chat.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(chat => chat.ContactId).IsUnique();
    }
}
