namespace VocaChat.WebApi.Dtos.AiAccounts;

/// <summary>
/// 表示客户端创建 AI 账号时允许提交的 HTTP 请求数据。
/// </summary>
public sealed class CreateAiAccountRequest
{
    public string? Nickname { get; set; }
    public string? IdentityDescription { get; set; }
    public string? Personality { get; set; }
    public string? SpeakingStyle { get; set; }
}
