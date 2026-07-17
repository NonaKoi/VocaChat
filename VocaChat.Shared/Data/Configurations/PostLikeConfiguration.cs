using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置动态点赞及本地用户的唯一点赞规则。
/// </summary>
public sealed class PostLikeConfiguration : IEntityTypeConfiguration<PostLike>
{
    public void Configure(EntityTypeBuilder<PostLike> builder)
    {
        builder.ToTable("PostLikes");
        builder.HasKey(like => like.Id);
        builder.Property(like => like.Id).ValueGeneratedNever();
        builder.Property(like => like.CreatedAt).IsRequired();
        builder.HasOne<Post>()
            .WithMany(post => post.Likes)
            .HasForeignKey(like => like.PostId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AiAccount>()
            .WithMany()
            .HasForeignKey(like => like.AiAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(like => new { like.PostId, like.AiAccountId })
            .IsUnique()
            .HasFilter("\"AiAccountId\" IS NOT NULL");
        builder.HasIndex(like => like.PostId)
            .IsUnique()
            .HasFilter("\"AiAccountId\" IS NULL");
    }
}
