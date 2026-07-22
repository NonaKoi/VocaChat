using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.Settings;

namespace VocaChat.WebApi.Controllers;

/// <summary>
/// 提供单个 AI 账号专有模型接口设置的读取和保存 API。
/// </summary>
[ApiController]
[Route("api/ai-accounts/{aiAccountId}/model-settings")]
public sealed class AiAccountModelConnectionSettingsController : ControllerBase
{
    private readonly AiModelConnectionSettingsService _settingsService;

    public AiAccountModelConnectionSettingsController(
        AiModelConnectionSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(AiAccountModelConnectionSettingsResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<AiAccountModelConnectionSettingsResponse> Get(
        Guid aiAccountId)
    {
        bool succeeded = _settingsService.TryGetAccountSettings(
            aiAccountId,
            out AiAccountModelConnectionSettingsSnapshot? settings);

        return !succeeded || settings is null
            ? NotFound()
            : Ok(ToResponse(settings));
    }

    [HttpPut]
    [ProducesResponseType(
        typeof(AiAccountModelConnectionSettingsResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<AiAccountModelConnectionSettingsResponse> Update(
        Guid aiAccountId,
        [FromBody] UpdateAiAccountModelConnectionSettingsRequest request)
    {
        if (!_settingsService.TryGetAccountSettings(aiAccountId, out _))
        {
            return NotFound();
        }

        bool succeeded = _settingsService.TryUpdateAccountSettings(
            aiAccountId,
            request.UseGlobalSettings,
            request.BaseUrl,
            request.Model,
            request.ApiKey,
            request.ClearApiKey,
            out AiAccountModelConnectionSettingsSnapshot? settings,
            out string errorMessage);

        if (!succeeded || settings is null)
        {
            return BadRequest(new { message = errorMessage });
        }

        return Ok(ToResponse(settings));
    }

    private static AiAccountModelConnectionSettingsResponse ToResponse(
        AiAccountModelConnectionSettingsSnapshot settings)
    {
        return new AiAccountModelConnectionSettingsResponse(
            settings.AiAccountId,
            settings.UseGlobalSettings,
            settings.BaseUrl,
            settings.Model,
            settings.HasApiKey,
            settings.EffectiveBaseUrl,
            settings.EffectiveModel,
            settings.EffectiveHasApiKey);
    }
}
