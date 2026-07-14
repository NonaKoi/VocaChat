using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.ConsoleApp.Models;

namespace VocaChat.ConsoleApp.Data.Configurations;

/// <summary>
/// 配置群聊字段，以及群聊和已有 AI 账号之间的直接多对多关系。
/// </summary>
public sealed class GroupChatConfiguration : IEntityTypeConfiguration<GroupChat>
{
    public void Configure(EntityTypeBuilder<GroupChat> builder)
    {
        builder.ToTable("GroupChats", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_GroupChats_Name_NotBlank",
                "length(trim(\"Name\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_GroupChats_Name_MaxLength",
                $"length(\"Name\") <= {GroupChat.NameMaxLength}");
        });

        builder.HasKey(groupChat => groupChat.Id);

        builder.Property(groupChat => groupChat.Id)
            .ValueGeneratedNever();

        builder.Property(groupChat => groupChat.Name)
            .IsRequired()
            .HasMaxLength(GroupChat.NameMaxLength);

        builder.Property(groupChat => groupChat.CreatedAt)
            .IsRequired();

        builder.HasMany(groupChat => groupChat.Members)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "GroupChatMembers",
                rightRelationship => rightRelationship
                    .HasOne<AiAccount>()
                    .WithMany()
                    .HasForeignKey("AiAccountId")
                    .OnDelete(DeleteBehavior.Restrict),
                leftRelationship => leftRelationship
                    .HasOne<GroupChat>()
                    .WithMany()
                    .HasForeignKey("GroupChatId")
                    .OnDelete(DeleteBehavior.Cascade),
                joinEntity =>
                {
                    joinEntity.ToTable("GroupChatMembers");
                    joinEntity.HasKey("GroupChatId", "AiAccountId");
                    joinEntity.HasIndex("AiAccountId");
                });

        builder.Navigation(groupChat => groupChat.Members)
            .HasField("_members")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
