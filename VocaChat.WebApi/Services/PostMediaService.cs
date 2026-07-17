using VocaChat.Models;
using VocaChat.Services;

namespace VocaChat.WebApi.Services;

/// <summary>
/// 协调动态图片文件保存和数据库媒体关系创建。
/// </summary>
public sealed class PostMediaService
{
    public const long MaximumImageLength = 10 * 1024 * 1024;

    private readonly PostService _postService;
    private readonly LocalMediaStorageService _storageService;

    public PostMediaService(
        PostService postService,
        LocalMediaStorageService storageService)
    {
        _postService = postService;
        _storageService = storageService;
    }

    public async Task<PostMediaUploadResult> UploadAsync(
        Guid postId,
        Stream source,
        long declaredLength,
        CancellationToken cancellationToken)
    {
        if (_postService.FindById(postId) is null)
        {
            return PostMediaUploadResult.Failed(
                PostMediaUploadStatus.PostNotFound,
                "动态不存在。");
        }

        LocalMediaSaveResult saveResult = await _storageService.SaveImageAsync(
            postId,
            LocalMediaKind.PostImage,
            source,
            declaredLength,
            MaximumImageLength,
            cancellationToken);

        if (saveResult.Status != LocalMediaSaveStatus.Succeeded
            || saveResult.StoredFile is null)
        {
            return PostMediaUploadResult.Failed(
                saveResult.Status switch
                {
                    LocalMediaSaveStatus.Empty => PostMediaUploadStatus.Empty,
                    LocalMediaSaveStatus.TooLarge => PostMediaUploadStatus.TooLarge,
                    LocalMediaSaveStatus.UnsupportedFormat =>
                        PostMediaUploadStatus.UnsupportedFormat,
                    _ => PostMediaUploadStatus.StorageFailed
                },
                saveResult.ErrorMessage);
        }

        if (!_postService.TryAddImage(
                postId,
                saveResult.StoredFile.MediaId,
                out PostImage? image,
                out string errorMessage)
            || image is null)
        {
            _storageService.Delete(
                postId,
                LocalMediaKind.PostImage,
                saveResult.StoredFile.MediaId);

            return PostMediaUploadResult.Failed(
                PostMediaUploadStatus.PersistenceFailed,
                errorMessage);
        }

        return PostMediaUploadResult.Succeeded(image);
    }

    public StoredMediaContent? OpenRead(Guid postId, Guid imageId)
    {
        Post? post = _postService.FindById(postId);
        PostImage? image = post?.Images.FirstOrDefault(candidate =>
            candidate.Id == imageId);

        return image is null
            ? null
            : _storageService.OpenRead(
                postId,
                LocalMediaKind.PostImage,
                image.MediaId);
    }
}

public enum PostMediaUploadStatus
{
    Succeeded,
    PostNotFound,
    Empty,
    TooLarge,
    UnsupportedFormat,
    StorageFailed,
    PersistenceFailed
}

public sealed class PostMediaUploadResult
{
    public PostMediaUploadStatus Status { get; }
    public PostImage? Image { get; }
    public string ErrorMessage { get; }

    private PostMediaUploadResult(
        PostMediaUploadStatus status,
        PostImage? image,
        string errorMessage)
    {
        Status = status;
        Image = image;
        ErrorMessage = errorMessage;
    }

    public static PostMediaUploadResult Succeeded(PostImage image) =>
        new(PostMediaUploadStatus.Succeeded, image, string.Empty);

    public static PostMediaUploadResult Failed(
        PostMediaUploadStatus status,
        string errorMessage) =>
        new(status, null, errorMessage);
}
