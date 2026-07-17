using VocaChat.Models;
using VocaChat.Services;

namespace VocaChat.WebApi.Services;

/// <summary>
/// 协调图片文件保存、账号媒体标识更新和旧文件清理。
/// </summary>
public sealed class AiAccountMediaService
{
    public const long AvatarMaximumLength = 5 * 1024 * 1024;
    public const long CoverMaximumLength = 10 * 1024 * 1024;

    private readonly AiAccountService _aiAccountService;
    private readonly LocalMediaStorageService _mediaStorageService;

    public AiAccountMediaService(
        AiAccountService aiAccountService,
        LocalMediaStorageService mediaStorageService)
    {
        _aiAccountService = aiAccountService;
        _mediaStorageService = mediaStorageService;
    }

    /// <summary>
    /// 保存新图片并在数据库成功切换标识后清理旧文件。
    /// </summary>
    public async Task<AiAccountMediaUploadResult> UploadAsync(
        Guid aiAccountId,
        LocalMediaKind mediaKind,
        Stream source,
        long declaredLength,
        CancellationToken cancellationToken)
    {
        AiAccount? existingAccount = _aiAccountService.FindById(aiAccountId);

        if (existingAccount is null)
        {
            return AiAccountMediaUploadResult.Failed(
                AiAccountMediaUploadStatus.AccountNotFound,
                "AI 账号不存在。");
        }

        long maximumLength = mediaKind == LocalMediaKind.Avatar
            ? AvatarMaximumLength
            : CoverMaximumLength;
        LocalMediaSaveResult saveResult = await _mediaStorageService
            .SaveImageAsync(
                aiAccountId,
                mediaKind,
                source,
                declaredLength,
                maximumLength,
                cancellationToken);

        if (saveResult.Status != LocalMediaSaveStatus.Succeeded
            || saveResult.StoredFile is null)
        {
            return AiAccountMediaUploadResult.Failed(
                ToUploadStatus(saveResult.Status),
                saveResult.ErrorMessage);
        }

        StoredMediaFile storedFile = saveResult.StoredFile;
        bool updated = mediaKind == LocalMediaKind.Avatar
            ? _aiAccountService.TryChangeAvatarMediaId(
                aiAccountId,
                storedFile.MediaId,
                out AiAccount? updatedAccount,
                out string? previousMediaId,
                out string errorMessage)
            : _aiAccountService.TryChangeProfileCoverMediaId(
                aiAccountId,
                storedFile.MediaId,
                out updatedAccount,
                out previousMediaId,
                out errorMessage);

        if (!updated || updatedAccount is null)
        {
            _mediaStorageService.Delete(
                aiAccountId,
                mediaKind,
                storedFile.MediaId);

            AiAccountMediaUploadStatus failureStatus =
                _aiAccountService.FindById(aiAccountId) is null
                    ? AiAccountMediaUploadStatus.AccountNotFound
                    : AiAccountMediaUploadStatus.PersistenceFailed;

            return AiAccountMediaUploadResult.Failed(
                failureStatus,
                errorMessage);
        }

        _mediaStorageService.Delete(
            aiAccountId,
            mediaKind,
            previousMediaId);

        return AiAccountMediaUploadResult.Succeeded(updatedAccount);
    }

    /// <summary>
    /// 按账号当前数据库标识打开头像或封面文件。
    /// </summary>
    public AiAccountMediaReadResult OpenRead(
        Guid aiAccountId,
        LocalMediaKind mediaKind)
    {
        AiAccount? aiAccount = _aiAccountService.FindById(aiAccountId);

        if (aiAccount is null)
        {
            return AiAccountMediaReadResult.NotFound();
        }

        string? mediaId = mediaKind == LocalMediaKind.Avatar
            ? aiAccount.AvatarMediaId
            : aiAccount.ProfileCoverMediaId;

        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return AiAccountMediaReadResult.NotFound();
        }

        StoredMediaContent? content = _mediaStorageService.OpenRead(
            aiAccountId,
            mediaKind,
            mediaId);

        return content is null
            ? AiAccountMediaReadResult.NotFound()
            : AiAccountMediaReadResult.Succeeded(mediaId, content);
    }

    private static AiAccountMediaUploadStatus ToUploadStatus(
        LocalMediaSaveStatus status)
    {
        return status switch
        {
            LocalMediaSaveStatus.Empty => AiAccountMediaUploadStatus.Empty,
            LocalMediaSaveStatus.TooLarge => AiAccountMediaUploadStatus.TooLarge,
            LocalMediaSaveStatus.UnsupportedFormat =>
                AiAccountMediaUploadStatus.UnsupportedFormat,
            _ => AiAccountMediaUploadStatus.StorageFailed
        };
    }
}

public enum AiAccountMediaUploadStatus
{
    Succeeded,
    AccountNotFound,
    Empty,
    TooLarge,
    UnsupportedFormat,
    StorageFailed,
    PersistenceFailed
}

public sealed class AiAccountMediaUploadResult
{
    public AiAccountMediaUploadStatus Status { get; }
    public AiAccount? AiAccount { get; }
    public string ErrorMessage { get; }

    private AiAccountMediaUploadResult(
        AiAccountMediaUploadStatus status,
        AiAccount? aiAccount,
        string errorMessage)
    {
        Status = status;
        AiAccount = aiAccount;
        ErrorMessage = errorMessage;
    }

    public static AiAccountMediaUploadResult Succeeded(AiAccount aiAccount)
    {
        return new AiAccountMediaUploadResult(
            AiAccountMediaUploadStatus.Succeeded,
            aiAccount,
            string.Empty);
    }

    public static AiAccountMediaUploadResult Failed(
        AiAccountMediaUploadStatus status,
        string errorMessage)
    {
        return new AiAccountMediaUploadResult(status, null, errorMessage);
    }
}

public sealed class AiAccountMediaReadResult
{
    public string? MediaId { get; }
    public StoredMediaContent? Content { get; }

    private AiAccountMediaReadResult(
        string? mediaId,
        StoredMediaContent? content)
    {
        MediaId = mediaId;
        Content = content;
    }

    public static AiAccountMediaReadResult Succeeded(
        string mediaId,
        StoredMediaContent content)
    {
        return new AiAccountMediaReadResult(mediaId, content);
    }

    public static AiAccountMediaReadResult NotFound()
    {
        return new AiAccountMediaReadResult(null, null);
    }
}
