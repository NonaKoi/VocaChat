using Microsoft.AspNetCore.Http;

namespace VocaChat.WebApi.Dtos.Posts;

/// <summary>上传一张动态图片所需的 multipart 表单。</summary>
public sealed class UploadPostImageRequest
{
    public IFormFile? File { get; init; }
}
