using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.Settings;

namespace VocaChat.WebApi.Controllers;

/// <summary>
/// 提供好友自主互动全局设置的读取和保存 API。
/// </summary>
[ApiController]
[Route("api/settings/autonomous-interactions")]
public class AutonomousInteractionSettingsController : ControllerBase
{
    private readonly AutonomousInteractionSettingsService _settingsService;

    public AutonomousInteractionSettingsController(
        AutonomousInteractionSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// 返回当前设置；尚未保存过时返回安全默认值。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(
        typeof(AutonomousInteractionSettingsResponse),
        StatusCodes.Status200OK)]
    public ActionResult<AutonomousInteractionSettingsResponse> Get()
    {
        AutonomousInteractionSettings settings = _settingsService.GetSettings();
        return Ok(ToResponse(settings));
    }

    /// <summary>
    /// 验证并保存当前本地用户的好友自主互动全局设置。
    /// </summary>
    [HttpPut]
    [ProducesResponseType(
        typeof(AutonomousInteractionSettingsResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<AutonomousInteractionSettingsResponse> Update(
        [FromBody] UpdateAutonomousInteractionSettingsRequest request)
    {
        if (!Enum.TryParse(
                request.Frequency,
                ignoreCase: true,
                out AutonomousInteractionFrequency frequency)
            || !Enum.IsDefined(frequency))
        {
            return BadRequest(new { message = "自主互动频率无效。" });
        }

        bool succeeded = _settingsService.TryUpdateSettings(
            request.IsEnabled,
            frequency,
            request.AllowPrivateChats,
            request.AllowGroupChats,
            out AutonomousInteractionSettings? settings,
            out string errorMessage);

        if (!succeeded || settings is null)
        {
            return BadRequest(new { message = errorMessage });
        }

        return Ok(ToResponse(settings));
    }

    private static AutonomousInteractionSettingsResponse ToResponse(
        AutonomousInteractionSettings settings)
    {
        return new AutonomousInteractionSettingsResponse
        {
            IsEnabled = settings.IsEnabled,
            Frequency = settings.Frequency.ToString(),
            AllowPrivateChats = settings.AllowPrivateChats,
            AllowGroupChats = settings.AllowGroupChats
        };
    }
}
