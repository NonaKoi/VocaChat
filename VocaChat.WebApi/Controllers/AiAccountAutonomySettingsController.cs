using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.Settings;

namespace VocaChat.WebApi.Controllers;

/// <summary>
/// 提供单个好友自主互动设置的读取和保存 API。
/// </summary>
[ApiController]
[Route("api/ai-accounts/{aiAccountId}/autonomy-settings")]
public class AiAccountAutonomySettingsController : ControllerBase
{
    private readonly AiAccountAutonomySettingsService _settingsService;

    public AiAccountAutonomySettingsController(
        AiAccountAutonomySettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// 返回已有好友的专有设置；好友不存在时返回 404。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(
        typeof(AiAccountAutonomySettingsResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<AiAccountAutonomySettingsResponse> Get(
        Guid aiAccountId)
    {
        bool succeeded = _settingsService.TryGetSettings(
            aiAccountId,
            out AiAccountAutonomySettings? settings);

        if (!succeeded || settings is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(settings));
    }

    /// <summary>
    /// 验证并保存已有好友的专有设置。
    /// </summary>
    [HttpPut]
    [ProducesResponseType(
        typeof(AiAccountAutonomySettingsResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<AiAccountAutonomySettingsResponse> Update(
        Guid aiAccountId,
        [FromBody] UpdateAiAccountAutonomySettingsRequest request)
    {
        if (!Enum.TryParse(
                request.InitiativeLevel,
                ignoreCase: true,
                out AutonomousInteractionInitiativeLevel initiativeLevel)
            || !Enum.IsDefined(initiativeLevel))
        {
            return BadRequest(new { message = "主动程度无效。" });
        }

        bool succeeded = _settingsService.TryUpdateSettings(
            aiAccountId,
            request.IsEnabled,
            initiativeLevel,
            request.CanInitiatePrivateChats,
            request.CanInitiateGroupChats,
            request.CanJoinGroupChats,
            out AiAccountAutonomySettings? settings);

        if (!succeeded || settings is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(settings));
    }

    private static AiAccountAutonomySettingsResponse ToResponse(
        AiAccountAutonomySettings settings)
    {
        return new AiAccountAutonomySettingsResponse
        {
            AiAccountId = settings.AiAccountId,
            IsEnabled = settings.IsEnabled,
            InitiativeLevel = settings.InitiativeLevel.ToString(),
            CanInitiatePrivateChats = settings.CanInitiatePrivateChats,
            CanInitiateGroupChats = settings.CanInitiateGroupChats,
            CanJoinGroupChats = settings.CanJoinGroupChats
        };
    }
}
