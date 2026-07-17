using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责好友动态、图片关系、点赞和评论的数据库业务。
/// </summary>
public sealed class PostService
{
    public const int MaximumImagesPerPost = 9;

    private readonly VocaChatDbContextFactory _dbContextFactory;

    public PostService(VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public bool TryCreatePost(
        Guid authorAiAccountId,
        string content,
        out Post? post,
        out string errorMessage)
    {
        post = null;

        if (string.IsNullOrWhiteSpace(content))
        {
            errorMessage = "动态内容不能为空。";
            return false;
        }

        string trimmedContent = content.Trim();

        if (trimmedContent.Length > Post.ContentMaxLength)
        {
            errorMessage = $"动态内容不能超过 {Post.ContentMaxLength} 个字符。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        if (!dbContext.Contacts.Any(contact =>
                contact.AiAccountId == authorAiAccountId))
        {
            errorMessage = "只有已有好友可以发布动态。";
            return false;
        }

        Post newPost = new(authorAiAccountId, trimmedContent);
        dbContext.Posts.Add(newPost);
        dbContext.SaveChanges();
        post = FindById(newPost.Id);
        errorMessage = string.Empty;
        return true;
    }

    public Post? FindById(Guid postId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return BuildPostQuery(dbContext)
            .FirstOrDefault(post => post.Id == postId);
    }

    public IReadOnlyList<Post> GetAllPosts()
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return BuildPostQuery(dbContext)
            .OrderByDescending(post => post.CreatedAt)
            .ThenBy(post => post.Id)
            .ToList()
            .AsReadOnly();
    }

    public bool TryAddImage(
        Guid postId,
        string mediaId,
        out PostImage? image,
        out string errorMessage)
    {
        image = null;

        if (string.IsNullOrWhiteSpace(mediaId)
            || mediaId.Length > PostImage.MediaIdMaxLength)
        {
            errorMessage = "动态图片标识无效。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        Post? post = dbContext.Posts
            .Include(candidate => candidate.Images)
            .FirstOrDefault(candidate => candidate.Id == postId);

        if (post is null)
        {
            errorMessage = "动态不存在。";
            return false;
        }

        if (post.Images.Count >= MaximumImagesPerPost)
        {
            errorMessage = $"每条动态最多包含 {MaximumImagesPerPost} 张图片。";
            return false;
        }

        int nextDisplayOrder = post.Images.Count == 0
            ? 0
            : post.Images.Max(existingImage => existingImage.DisplayOrder) + 1;
        image = post.AddImage(mediaId, nextDisplayOrder);
        dbContext.SaveChanges();
        errorMessage = string.Empty;
        return true;
    }

    public bool TryAddLocalUserLike(Guid postId, out string errorMessage)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        Post? post = dbContext.Posts
            .Include(candidate => candidate.Likes)
            .FirstOrDefault(candidate => candidate.Id == postId);

        if (post is null)
        {
            errorMessage = "动态不存在。";
            return false;
        }

        if (post.Likes.Any(like => like.AiAccountId is null))
        {
            errorMessage = string.Empty;
            return true;
        }

        post.AddLocalUserLike();
        dbContext.SaveChanges();
        errorMessage = string.Empty;
        return true;
    }

    public bool TryRemoveLocalUserLike(Guid postId, out string errorMessage)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        if (!dbContext.Posts.Any(post => post.Id == postId))
        {
            errorMessage = "动态不存在。";
            return false;
        }

        PostLike? like = dbContext.PostLikes.FirstOrDefault(candidate =>
            candidate.PostId == postId && candidate.AiAccountId == null);

        if (like is not null)
        {
            dbContext.PostLikes.Remove(like);
            dbContext.SaveChanges();
        }

        errorMessage = string.Empty;
        return true;
    }

    public bool TryAddLocalUserComment(
        Guid postId,
        string content,
        out PostComment? comment,
        out string errorMessage)
    {
        comment = null;

        if (string.IsNullOrWhiteSpace(content))
        {
            errorMessage = "评论内容不能为空。";
            return false;
        }

        string trimmedContent = content.Trim();

        if (trimmedContent.Length > PostComment.ContentMaxLength)
        {
            errorMessage = $"评论内容不能超过 {PostComment.ContentMaxLength} 个字符。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        Post? post = dbContext.Posts
            .FirstOrDefault(candidate => candidate.Id == postId);

        if (post is null)
        {
            errorMessage = "动态不存在。";
            return false;
        }

        comment = post.AddLocalUserComment(trimmedContent);
        dbContext.SaveChanges();
        errorMessage = string.Empty;
        return true;
    }

    private static IQueryable<Post> BuildPostQuery(
        VocaChatDbContext dbContext)
    {
        return dbContext.Posts
            .AsNoTracking()
            .Include(post => post.Author)
                .ThenInclude(author => author.Tags)
            .Include(post => post.Images)
            .Include(post => post.Likes)
            .Include(post => post.Comments)
            .AsSplitQuery();
    }
}
