using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置好友动态及作者关系。
/// </summary>
public sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_Posts_Content_NotBlank",
                "length(trim(\"Content\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_Posts_Content_MaxLength",
                $"length(\"Content\") <= {Post.ContentMaxLength}");
        });

        builder.HasKey(post => post.Id);
        builder.Property(post => post.Id).ValueGeneratedNever();
        builder.Property(post => post.Content)
            .IsRequired()
            .HasMaxLength(Post.ContentMaxLength);
        builder.Property(post => post.CreatedAt).IsRequired();
        builder.HasOne(post => post.Author)
            .WithMany()
            .HasForeignKey(post => post.AuthorAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(post => new { post.CreatedAt, post.Id });

        builder.Navigation(post => post.Images)
            .HasField("_images")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(post => post.Likes)
            .HasField("_likes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(post => post.Comments)
            .HasField("_comments")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
