using System.Security.Cryptography;
using System.Text;

namespace VocaChat.Services;

/// <summary>
/// 使用 Windows 当前用户范围的 DPAPI 保护保存在本地数据库中的 API Key。
/// </summary>
public sealed class AiApiKeyProtector
{
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("VocaChat.AiModelConnection.ApiKey.v1");

    public string Protect(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "VocaChat 当前使用 Windows DPAPI 保存本地 API Key。");
        }

        byte[] plaintext = Encoding.UTF8.GetBytes(apiKey);
        byte[] ciphertext = ProtectedData.Protect(
            plaintext,
            Entropy,
            DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(ciphertext);
    }

    public string? Unprotect(string? protectedApiKey)
    {
        if (string.IsNullOrWhiteSpace(protectedApiKey))
        {
            return null;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "VocaChat 当前使用 Windows DPAPI 保存本地 API Key。");
        }

        try
        {
            byte[] ciphertext = Convert.FromBase64String(protectedApiKey);
            byte[] plaintext = ProtectedData.Unprotect(
                ciphertext,
                Entropy,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException exception)
        {
            throw new AiMessageGenerationException(
                "无法读取已保存的 API Key，请在设置中重新填写。",
                exception);
        }
        catch (FormatException exception)
        {
            throw new AiMessageGenerationException(
                "已保存的 API Key 格式无效，请在设置中重新填写。",
                exception);
        }
    }
}
