using System.Buffers;

namespace VocaChat.WebApi.Services;

/// <summary>
/// 将经过限制的账号图片保存到本地应用数据目录，不向数据库暴露绝对路径。
/// </summary>
public sealed class LocalMediaStorageService
{
    private const int BufferSize = 81920;

    private readonly string _rootDirectory;

    public LocalMediaStorageService(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("媒体根目录不能为空。", nameof(rootDirectory));
        }

        _rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(_rootDirectory);
    }

    /// <summary>
    /// 在流式复制时限制文件大小，并根据文件头选择安全扩展名和 Content-Type。
    /// </summary>
    public async Task<LocalMediaSaveResult> SaveImageAsync(
        Guid aiAccountId,
        LocalMediaKind mediaKind,
        Stream source,
        long declaredLength,
        long maximumLength,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (declaredLength <= 0)
        {
            return LocalMediaSaveResult.Rejected(
                LocalMediaSaveStatus.Empty,
                "请选择一个非空图片文件。");
        }

        if (declaredLength > maximumLength)
        {
            return LocalMediaSaveResult.Rejected(
                LocalMediaSaveStatus.TooLarge,
                "图片文件超过允许的大小限制。");
        }

        string mediaDirectory = GetMediaDirectory(aiAccountId, mediaKind);
        Directory.CreateDirectory(mediaDirectory);

        string temporaryPath = Path.Combine(
            mediaDirectory,
            $".{Guid.NewGuid():N}.upload");
        byte[] header = new byte[12];
        int headerLength = 0;
        long totalLength = 0;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            await using (FileStream target = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                while (true)
                {
                    int bytesRead = await source.ReadAsync(
                        buffer.AsMemory(0, buffer.Length),
                        cancellationToken);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    totalLength += bytesRead;

                    if (totalLength > maximumLength)
                    {
                        return LocalMediaSaveResult.Rejected(
                            LocalMediaSaveStatus.TooLarge,
                            "图片文件超过允许的大小限制。");
                    }

                    if (headerLength < header.Length)
                    {
                        int copyLength = Math.Min(
                            header.Length - headerLength,
                            bytesRead);
                        buffer.AsSpan(0, copyLength).CopyTo(
                            header.AsSpan(headerLength));
                        headerLength += copyLength;
                    }

                    await target.WriteAsync(
                        buffer.AsMemory(0, bytesRead),
                        cancellationToken);
                }

                await target.FlushAsync(cancellationToken);
            }

            if (totalLength == 0)
            {
                return LocalMediaSaveResult.Rejected(
                    LocalMediaSaveStatus.Empty,
                    "请选择一个非空图片文件。");
            }

            ImageMediaFormat? imageFormat = DetectImageFormat(
                header.AsSpan(0, headerLength));

            if (imageFormat is null)
            {
                return LocalMediaSaveResult.Rejected(
                    LocalMediaSaveStatus.UnsupportedFormat,
                    "仅支持 JPEG、PNG 和 WebP 图片。");
            }

            string mediaId = $"{Guid.NewGuid():N}{imageFormat.Extension}";
            string finalPath = Path.Combine(mediaDirectory, mediaId);
            File.Move(temporaryPath, finalPath);

            return LocalMediaSaveResult.Succeeded(
                new StoredMediaFile(mediaId, imageFormat.ContentType));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            return LocalMediaSaveResult.Rejected(
                LocalMediaSaveStatus.StorageFailed,
                "图片暂时无法保存，请重试。");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);

            TryDeleteFile(temporaryPath);
        }
    }

    /// <summary>
    /// 根据数据库中的不透明标识打开图片；标识无效或文件缺失时返回 null。
    /// </summary>
    public StoredMediaContent? OpenRead(
        Guid aiAccountId,
        LocalMediaKind mediaKind,
        string mediaId)
    {
        if (!TryGetImageFormat(mediaId, out ImageMediaFormat imageFormat))
        {
            return null;
        }

        string path = Path.Combine(
            GetMediaDirectory(aiAccountId, mediaKind),
            mediaId);

        if (!File.Exists(path))
        {
            return null;
        }

        FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return new StoredMediaContent(stream, imageFormat.ContentType);
    }

    /// <summary>
    /// 删除指定账号下的旧媒体；缺失文件视为已经完成清理。
    /// </summary>
    public bool Delete(
        Guid aiAccountId,
        LocalMediaKind mediaKind,
        string? mediaId)
    {
        if (string.IsNullOrWhiteSpace(mediaId)
            || !TryGetImageFormat(mediaId, out _))
        {
            return false;
        }

        string path = Path.Combine(
            GetMediaDirectory(aiAccountId, mediaKind),
            mediaId);

        return !File.Exists(path) || TryDeleteFile(path);
    }

    private string GetMediaDirectory(
        Guid aiAccountId,
        LocalMediaKind mediaKind)
    {
        return mediaKind switch
        {
            LocalMediaKind.Avatar => Path.Combine(
                _rootDirectory,
                "AiAccounts",
                aiAccountId.ToString("N"),
                "avatar"),
            LocalMediaKind.ProfileCover => Path.Combine(
                _rootDirectory,
                "AiAccounts",
                aiAccountId.ToString("N"),
                "cover"),
            LocalMediaKind.PostImage => Path.Combine(
                _rootDirectory,
                "Posts",
                aiAccountId.ToString("N"),
                "images"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(mediaKind),
                mediaKind,
                "未知的本地媒体类型。")
        };
    }

    private static ImageMediaFormat? DetectImageFormat(
        ReadOnlySpan<byte> header)
    {
        if (header.Length >= 8
            && header[..8].SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ImageMediaFormat.Png;
        }

        if (header.Length >= 3
            && header[0] == 0xFF
            && header[1] == 0xD8
            && header[2] == 0xFF)
        {
            return ImageMediaFormat.Jpeg;
        }

        if (header.Length >= 12
            && header[..4].SequenceEqual("RIFF"u8)
            && header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return ImageMediaFormat.WebP;
        }

        return null;
    }

    private static bool TryGetImageFormat(
        string mediaId,
        out ImageMediaFormat imageFormat)
    {
        imageFormat = null!;

        if (!string.Equals(
                Path.GetFileName(mediaId),
                mediaId,
                StringComparison.Ordinal))
        {
            return false;
        }

        string extension = Path.GetExtension(mediaId);
        string identifier = Path.GetFileNameWithoutExtension(mediaId);

        if (!Guid.TryParseExact(identifier, "N", out _))
        {
            return false;
        }

        ImageMediaFormat? detectedFormat = extension.ToLowerInvariant() switch
        {
            ".png" => ImageMediaFormat.Png,
            ".jpg" => ImageMediaFormat.Jpeg,
            ".webp" => ImageMediaFormat.WebP,
            _ => null
        };

        if (detectedFormat is null)
        {
            return false;
        }

        imageFormat = detectedFormat;
        return true;
    }

    private static bool TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return true;
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record ImageMediaFormat(
        string Extension,
        string ContentType)
    {
        public static ImageMediaFormat Png { get; } = new(".png", "image/png");
        public static ImageMediaFormat Jpeg { get; } = new(".jpg", "image/jpeg");
        public static ImageMediaFormat WebP { get; } = new(".webp", "image/webp");
    }
}

public enum LocalMediaSaveStatus
{
    Succeeded,
    Empty,
    TooLarge,
    UnsupportedFormat,
    StorageFailed
}

public sealed record StoredMediaFile(string MediaId, string ContentType);

public sealed record StoredMediaContent(Stream Stream, string ContentType);

public sealed class LocalMediaSaveResult
{
    public LocalMediaSaveStatus Status { get; }
    public StoredMediaFile? StoredFile { get; }
    public string ErrorMessage { get; }

    private LocalMediaSaveResult(
        LocalMediaSaveStatus status,
        StoredMediaFile? storedFile,
        string errorMessage)
    {
        Status = status;
        StoredFile = storedFile;
        ErrorMessage = errorMessage;
    }

    public static LocalMediaSaveResult Succeeded(StoredMediaFile storedFile)
    {
        return new LocalMediaSaveResult(
            LocalMediaSaveStatus.Succeeded,
            storedFile,
            string.Empty);
    }

    public static LocalMediaSaveResult Rejected(
        LocalMediaSaveStatus status,
        string errorMessage)
    {
        return new LocalMediaSaveResult(status, null, errorMessage);
    }
}
