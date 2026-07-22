using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责保存模型接口设置，并在每次模型调用前解析全局或账号专有连接。
/// </summary>
public sealed class AiModelConnectionSettingsService
{
    internal const int MaximumBaseUrlLength = 2048;
    internal const int MaximumModelLength = 200;
    internal const int MaximumApiKeyLength = 4096;

    private readonly VocaChatDbContextFactory _dbContextFactory;
    private readonly AiMessageGenerationOptions _hostDefaults;
    private readonly AiApiKeyProtector _apiKeyProtector;

    public AiModelConnectionSettingsService(
        VocaChatDbContextFactory dbContextFactory,
        AiMessageGenerationOptions hostDefaults,
        AiApiKeyProtector apiKeyProtector)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _hostDefaults = hostDefaults
            ?? throw new ArgumentNullException(nameof(hostDefaults));
        _apiKeyProtector = apiKeyProtector
            ?? throw new ArgumentNullException(nameof(apiKeyProtector));
    }

    /// <summary>
    /// 返回数据库中的全局设置；尚未保存时返回启动配置的安全摘要。
    /// </summary>
    public AiModelConnectionSettingsSnapshot GetGlobalSettings()
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AiModelConnectionSettings? storedSettings = dbContext
            .AiModelConnectionSettings
            .AsNoTracking()
            .SingleOrDefault(settings =>
                settings.Id == AiModelConnectionSettings.SingletonId);

        return storedSettings is null
            ? new AiModelConnectionSettingsSnapshot(
                NormalizeHostBaseUrl(),
                _hostDefaults.Model.Trim(),
                !string.IsNullOrWhiteSpace(_hostDefaults.ApiKey))
            : ToSnapshot(storedSettings);
    }

    /// <summary>
    /// 验证并保存全局模型接口。空白 API Key 表示保留，clearApiKey 表示明确清除。
    /// </summary>
    public bool TryUpdateGlobalSettings(
        string? baseUrl,
        string? model,
        string? apiKey,
        bool clearApiKey,
        out AiModelConnectionSettingsSnapshot? settings,
        out string errorMessage)
    {
        settings = null;

        if (!TryNormalizeConnection(
                baseUrl,
                model,
                out string normalizedBaseUrl,
                out string normalizedModel,
                out errorMessage)
            || !TryValidateApiKey(apiKey, clearApiKey, out errorMessage))
        {
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AiModelConnectionSettings? storedSettings = dbContext
            .AiModelConnectionSettings
            .SingleOrDefault(item =>
                item.Id == AiModelConnectionSettings.SingletonId);

        string? protectedApiKey = ResolveUpdatedProtectedApiKey(
            storedSettings?.ProtectedApiKey,
            apiKey,
            clearApiKey,
            fallbackApiKey: storedSettings is null ? _hostDefaults.ApiKey : null);

        if (storedSettings is null)
        {
            storedSettings = new AiModelConnectionSettings(
                normalizedBaseUrl,
                normalizedModel,
                protectedApiKey);
            dbContext.AiModelConnectionSettings.Add(storedSettings);
        }
        else
        {
            storedSettings.Update(
                normalizedBaseUrl,
                normalizedModel,
                protectedApiKey);
        }

        dbContext.SaveChanges();
        settings = ToSnapshot(storedSettings);
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 返回一个已有 AI 账号的专有设置，以及继承规则计算后的实际连接摘要。
    /// </summary>
    public bool TryGetAccountSettings(
        Guid aiAccountId,
        out AiAccountModelConnectionSettingsSnapshot? settings)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        if (!dbContext.AiAccounts.Any(account => account.Id == aiAccountId))
        {
            settings = null;
            return false;
        }

        AiModelConnection globalConnection = ResolveGlobal(dbContext);
        AiAccountModelConnectionSettings? storedSettings = dbContext
            .AiAccountModelConnectionSettings
            .AsNoTracking()
            .SingleOrDefault(item => item.AiAccountId == aiAccountId);

        settings = storedSettings is null
            ? new AiAccountModelConnectionSettingsSnapshot(
                aiAccountId,
                UseGlobalSettings: true,
                globalConnection.BaseUrl,
                globalConnection.Model,
                HasApiKey: false,
                globalConnection.BaseUrl,
                globalConnection.Model,
                !string.IsNullOrWhiteSpace(globalConnection.ApiKey))
            : ToSnapshot(storedSettings, globalConnection);
        return true;
    }

    /// <summary>
    /// 保存账号专有接口；关闭继承时，专有地址、模型和密钥作为完整配置覆盖全局。
    /// </summary>
    public bool TryUpdateAccountSettings(
        Guid aiAccountId,
        bool useGlobalSettings,
        string? baseUrl,
        string? model,
        string? apiKey,
        bool clearApiKey,
        out AiAccountModelConnectionSettingsSnapshot? settings,
        out string errorMessage)
    {
        settings = null;

        if (!TryValidateApiKey(apiKey, clearApiKey, out errorMessage))
        {
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        if (!dbContext.AiAccounts.Any(account => account.Id == aiAccountId))
        {
            errorMessage = "好友不存在。";
            return false;
        }

        AiModelConnection globalConnection = ResolveGlobal(dbContext);
        string normalizedBaseUrl;
        string normalizedModel;

        if (useGlobalSettings)
        {
            normalizedBaseUrl = string.IsNullOrWhiteSpace(baseUrl)
                ? globalConnection.BaseUrl
                : NormalizeBaseUrlOrFallback(baseUrl, globalConnection.BaseUrl);
            normalizedModel = string.IsNullOrWhiteSpace(model)
                ? globalConnection.Model
                : model.Trim();
        }
        else if (!TryNormalizeConnection(
                     baseUrl,
                     model,
                     out normalizedBaseUrl,
                     out normalizedModel,
                     out errorMessage))
        {
            return false;
        }

        AiAccountModelConnectionSettings? storedSettings = dbContext
            .AiAccountModelConnectionSettings
            .SingleOrDefault(item => item.AiAccountId == aiAccountId);

        string? protectedApiKey = ResolveUpdatedProtectedApiKey(
            storedSettings?.ProtectedApiKey,
            apiKey,
            clearApiKey,
            fallbackApiKey: !useGlobalSettings && storedSettings is null
                ? globalConnection.ApiKey
                : null);

        if (storedSettings is null)
        {
            storedSettings = new AiAccountModelConnectionSettings(
                aiAccountId,
                normalizedBaseUrl,
                normalizedModel);
            dbContext.AiAccountModelConnectionSettings.Add(storedSettings);
        }

        storedSettings.Update(
            useGlobalSettings,
            normalizedBaseUrl,
            normalizedModel,
            protectedApiKey);

        dbContext.SaveChanges();
        settings = ToSnapshot(storedSettings, globalConnection);
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 返回系统级分析任务使用的全局连接。
    /// </summary>
    public AiModelConnection ResolveGlobal()
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        return ResolveGlobal(dbContext);
    }

    /// <summary>
    /// 根据发言账号解析连接；有效的专有设置优先于全局设置。
    /// </summary>
    public AiModelConnection ResolveForAccount(Guid aiAccountId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AiModelConnection globalConnection = ResolveGlobal(dbContext);
        AiAccountModelConnectionSettings? accountSettings = dbContext
            .AiAccountModelConnectionSettings
            .AsNoTracking()
            .SingleOrDefault(item => item.AiAccountId == aiAccountId);

        if (accountSettings is null || accountSettings.UseGlobalSettings)
        {
            return globalConnection;
        }

        return new AiModelConnection(
            accountSettings.BaseUrl,
            accountSettings.Model,
            _apiKeyProtector.Unprotect(accountSettings.ProtectedApiKey));
    }

    private AiModelConnection ResolveGlobal(VocaChatDbContext dbContext)
    {
        AiModelConnectionSettings? storedSettings = dbContext
            .AiModelConnectionSettings
            .AsNoTracking()
            .SingleOrDefault(settings =>
                settings.Id == AiModelConnectionSettings.SingletonId);

        return storedSettings is null
            ? new AiModelConnection(
                NormalizeHostBaseUrl(),
                _hostDefaults.Model.Trim(),
                string.IsNullOrWhiteSpace(_hostDefaults.ApiKey)
                    ? null
                    : _hostDefaults.ApiKey)
            : new AiModelConnection(
                storedSettings.BaseUrl,
                storedSettings.Model,
                _apiKeyProtector.Unprotect(storedSettings.ProtectedApiKey));
    }

    private string? ResolveUpdatedProtectedApiKey(
        string? currentProtectedApiKey,
        string? apiKey,
        bool clearApiKey,
        string? fallbackApiKey)
    {
        if (clearApiKey)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return _apiKeyProtector.Protect(apiKey.Trim());
        }

        if (!string.IsNullOrWhiteSpace(currentProtectedApiKey))
        {
            return currentProtectedApiKey;
        }

        return string.IsNullOrWhiteSpace(fallbackApiKey)
            ? null
            : _apiKeyProtector.Protect(fallbackApiKey.Trim());
    }

    private static bool TryNormalizeConnection(
        string? baseUrl,
        string? model,
        out string normalizedBaseUrl,
        out string normalizedModel,
        out string errorMessage)
    {
        normalizedBaseUrl = string.Empty;
        normalizedModel = string.Empty;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            errorMessage = "API 地址不能为空。";
            return false;
        }

        string trimmedBaseUrl = baseUrl.Trim();
        if (trimmedBaseUrl.Length > MaximumBaseUrlLength
            || !Uri.TryCreate(trimmedBaseUrl, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp
                && uri.Scheme != Uri.UriSchemeHttps))
        {
            errorMessage = "API 地址必须是有效的 HTTP 或 HTTPS 地址。";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            errorMessage = "API 地址不能包含查询参数或片段。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            errorMessage = "模型名称不能为空。";
            return false;
        }

        normalizedModel = model.Trim();
        if (normalizedModel.Length > MaximumModelLength)
        {
            errorMessage = $"模型名称不能超过 {MaximumModelLength} 个字符。";
            return false;
        }

        normalizedBaseUrl = trimmedBaseUrl.EndsWith('/')
            ? trimmedBaseUrl
            : $"{trimmedBaseUrl}/";
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateApiKey(
        string? apiKey,
        bool clearApiKey,
        out string errorMessage)
    {
        if (clearApiKey && !string.IsNullOrWhiteSpace(apiKey))
        {
            errorMessage = "不能同时填写新 API Key 并清除现有密钥。";
            return false;
        }

        if (apiKey?.Trim().Length > MaximumApiKeyLength)
        {
            errorMessage = $"API Key 不能超过 {MaximumApiKeyLength} 个字符。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private string NormalizeHostBaseUrl()
    {
        return TryNormalizeConnection(
            _hostDefaults.BaseUrl,
            _hostDefaults.Model,
            out string normalizedBaseUrl,
            out _,
            out _)
            ? normalizedBaseUrl
            : "http://127.0.0.1:11434/v1/";
    }

    private static string NormalizeBaseUrlOrFallback(
        string baseUrl,
        string fallback)
    {
        return Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp
                || uri.Scheme == Uri.UriSchemeHttps)
            ? baseUrl.Trim().EndsWith('/')
                ? baseUrl.Trim()
                : $"{baseUrl.Trim()}/"
            : fallback;
    }

    private static AiModelConnectionSettingsSnapshot ToSnapshot(
        AiModelConnectionSettings settings)
    {
        return new AiModelConnectionSettingsSnapshot(
            settings.BaseUrl,
            settings.Model,
            !string.IsNullOrWhiteSpace(settings.ProtectedApiKey));
    }

    private static AiAccountModelConnectionSettingsSnapshot ToSnapshot(
        AiAccountModelConnectionSettings settings,
        AiModelConnection globalConnection)
    {
        bool usesGlobal = settings.UseGlobalSettings;
        return new AiAccountModelConnectionSettingsSnapshot(
            settings.AiAccountId,
            usesGlobal,
            settings.BaseUrl,
            settings.Model,
            !string.IsNullOrWhiteSpace(settings.ProtectedApiKey),
            usesGlobal ? globalConnection.BaseUrl : settings.BaseUrl,
            usesGlobal ? globalConnection.Model : settings.Model,
            usesGlobal
                ? !string.IsNullOrWhiteSpace(globalConnection.ApiKey)
                : !string.IsNullOrWhiteSpace(settings.ProtectedApiKey));
    }
}
