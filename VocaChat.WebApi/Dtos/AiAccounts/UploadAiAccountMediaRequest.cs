using Microsoft.AspNetCore.Http;

namespace VocaChat.WebApi.Dtos.AiAccounts;

/// <summary>
/// 表示通过 multipart/form-data 上传的一张账号图片。
/// </summary>
public sealed class UploadAiAccountMediaRequest
{
    public IFormFile? File { get; init; }
}
