using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置群消息写入时的 AI 接收者快照。
/// </summary>
public sealed class GroupMessageAudienceConfiguration
    : IEntityTypeConfiguration<GroupMessageAudience>
{
    public void Configure(EntityTypeBuilder<GroupMessageAudience> builder)
    {
        builder.ToTable("GroupMessageAudience");
        builder.HasKey(audience => new
        {
            audience.GroupMessageId,
            audience.AiAccountId
        });
        builder.Property(audience => audience.VisibleAt).IsRequired();

        builder.HasIndex(audience => new
        {
            audience.AiAccountId,
            audience.VisibleAt
        });

        builder.HasOne(audience => audience.GroupMessage)
            .WithMany()
            .HasForeignKey(audience => audience.GroupMessageId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(audience => audience.AiAccount)
            .WithMany()
            .HasForeignKey(audience => audience.AiAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
