using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置动态图片媒体标识和显示顺序。
/// </summary>
public sealed class PostImageConfiguration : IEntityTypeConfiguration<PostImage>
{
    public void Configure(EntityTypeBuilder<PostImage> builder)
    {
        builder.ToTable("PostImages");
        builder.HasKey(image => image.Id);
        builder.Property(image => image.Id).ValueGeneratedNever();
        builder.Property(image => image.MediaId)
            .IsRequired()
            .HasMaxLength(PostImage.MediaIdMaxLength);
        builder.Property(image => image.DisplayOrder).IsRequired();
        builder.HasOne<Post>()
            .WithMany(post => post.Images)
            .HasForeignKey(image => image.PostId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(image => new { image.PostId, image.DisplayOrder })
            .IsUnique();
    }
}
