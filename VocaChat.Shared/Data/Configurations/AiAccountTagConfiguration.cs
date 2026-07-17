using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置 AI 账号结构化标签的复合主键、查询索引和所属账号关系。
/// </summary>
public sealed class AiAccountTagConfiguration : IEntityTypeConfiguration<AiAccountTag>
{
    public void Configure(EntityTypeBuilder<AiAccountTag> builder)
    {
        builder.ToTable("AiAccountTags");

        builder.HasKey(tag => new
        {
            tag.AiAccountId,
            tag.Type,
            tag.Value
        });

        builder.Property(tag => tag.Type)
            .IsRequired();

        builder.Property(tag => tag.Value)
            .IsRequired()
            .HasMaxLength(AiAccountTag.ValueMaxLength)
            .UseCollation("NOCASE");

        builder.HasIndex(tag => new { tag.Type, tag.Value });

        builder.HasOne<AiAccount>()
            .WithMany(aiAccount => aiAccount.Tags)
            .HasForeignKey(tag => tag.AiAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
