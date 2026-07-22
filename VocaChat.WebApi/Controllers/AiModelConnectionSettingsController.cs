using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.Settings;

namespace VocaChat.WebApi.Controllers;

/// <summary>
/// 提供全局 AI 模型接口设置的读取和保存 API。
/// </summary>
[ApiController]
[Route("api/settings/ai-model")]
public sealed class AiModelConnectionSettingsController : ControllerBase
{
    private readonly AiModelConnectionSettingsService _settingsService;

    public AiModelConnectionSettingsController(
        AiModelConnectionSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(AiModelConnectionSettingsResponse),
        StatusCodes.Status200OK)]
    public ActionResult<AiModelConnectionSettingsResponse> Get()
    {
        return Ok(ToResponse(_settingsService.GetGlobalSettings()));
    }

    [HttpPut]
    [ProducesResponseType(
        typeof(AiModelConnectionSettingsResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<AiModelConnectionSettingsResponse> Update(
        [FromBody] UpdateAiModelConnectionSettingsRequest request)
    {
        bool succeeded = _settingsService.TryUpdateGlobalSettings(
            request.BaseUrl,
            request.Model,
            request.ApiKey,
            request.ClearApiKey,
            out AiModelConnectionSettingsSnapshot? settings,
            out string errorMessage);

        if (!succeeded || settings is null)
        {
            return BadRequest(new { message = errorMessage });
        }

        return Ok(ToResponse(settings));
    }

    private static AiModelConnectionSettingsResponse ToResponse(
        AiModelConnectionSettingsSnapshot settings)
    {
        return new AiModelConnectionSettingsResponse(
            settings.BaseUrl,
            settings.Model,
            settings.HasApiKey);
    }
}
