using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置动态评论内容、发送者快照和查询顺序。
/// </summary>
public sealed class PostCommentConfiguration
    : IEntityTypeConfiguration<PostComment>
{
    public void Configure(EntityTypeBuilder<PostComment> builder)
    {
        builder.ToTable("PostComments", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_PostComments_SenderDisplayName_NotBlank",
                "length(trim(\"SenderDisplayName\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_PostComments_Content_NotBlank",
                "length(trim(\"Content\")) > 0");
        });

        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.Id).ValueGeneratedNever();
        builder.Property(comment => comment.SenderDisplayName)
            .IsRequired()
            .HasMaxLength(PostComment.SenderDisplayNameMaxLength);
        builder.Property(comment => comment.Content)
            .IsRequired()
            .HasMaxLength(PostComment.ContentMaxLength);
        builder.Property(comment => comment.CreatedAt).IsRequired();
        builder.HasOne<Post>()
            .WithMany(post => post.Comments)
            .HasForeignKey(comment => comment.PostId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AiAccount>()
            .WithMany()
            .HasForeignKey(comment => comment.AiAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(comment => new
        {
            comment.PostId,
            comment.CreatedAt,
            comment.Id
        });
    }
}
