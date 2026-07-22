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

        if (!Enum.TryParse(
                request.ReplyDelayMode,
                ignoreCase: true,
                out AiReplyDelayMode replyDelayMode)
            || !Enum.IsDefined(replyDelayMode))
        {
            return BadRequest(new { message = "回复速度模式无效。" });
        }

        if (!Enum.TryParse(
                request.ConsecutiveMessageDelayMode,
                ignoreCase: true,
                out AiReplyDelayMode consecutiveMessageDelayMode)
            || !Enum.IsDefined(consecutiveMessageDelayMode))
        {
            return BadRequest(new { message = "连续消息间隔模式无效。" });
        }

        bool succeeded = _settingsService.TryUpdateSettings(
            request.IsEnabled,
            frequency,
            request.AllowPrivateChats,
            request.AllowGroupChats,
            request.PrivateChatContinuationRatePercent,
            request.PrivateChatMaximumRounds,
            request.AutonomousGroupChatMaximumMembers,
            request.GroupChatContinuationRatePercent,
            request.GroupChatMaximumRounds,
            replyDelayMode,
            request.FixedReplyDelayMilliseconds,
            request.MinimumReplyDelayMilliseconds,
            request.MaximumReplyDelayMilliseconds,
            consecutiveMessageDelayMode,
            request.FixedConsecutiveMessageDelayMilliseconds,
            request.MinimumConsecutiveMessageDelayMilliseconds,
            request.MaximumConsecutiveMessageDelayMilliseconds,
            request.MaximumConsecutiveQuestionTurns,
            request.MinimumReplyMessageCount,
            request.MaximumReplyMessageCount,
            request.GroupChatMaximumSpeakersPerTurn,
            request.GroupChatWholeGroupMaximumSpeakersPerTurn,
            request.GroupChatMaximumMessagesPerTurn,
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
            AllowGroupChats = settings.AllowGroupChats,
            PrivateChatContinuationRatePercent =
                settings.PrivateChatContinuationRatePercent,
            PrivateChatMaximumRounds = settings.PrivateChatMaximumRounds,
            AutonomousGroupChatMaximumMembers =
                settings.AutonomousGroupChatMaximumMembers,
            GroupChatContinuationRatePercent =
                settings.GroupChatContinuationRatePercent,
            GroupChatMaximumRounds = settings.GroupChatMaximumRounds,
            ReplyDelayMode = settings.ReplyDelayMode.ToString(),
            FixedReplyDelayMilliseconds = settings.FixedReplyDelayMilliseconds,
            MinimumReplyDelayMilliseconds =
                settings.MinimumReplyDelayMilliseconds,
            MaximumReplyDelayMilliseconds =
                settings.MaximumReplyDelayMilliseconds,
            ConsecutiveMessageDelayMode =
                settings.ConsecutiveMessageDelayMode.ToString(),
            FixedConsecutiveMessageDelayMilliseconds =
                settings.FixedConsecutiveMessageDelayMilliseconds,
            MinimumConsecutiveMessageDelayMilliseconds =
                settings.MinimumConsecutiveMessageDelayMilliseconds,
            MaximumConsecutiveMessageDelayMilliseconds =
                settings.MaximumConsecutiveMessageDelayMilliseconds,
            MaximumConsecutiveQuestionTurns =
                settings.MaximumConsecutiveQuestionTurns,
            MinimumReplyMessageCount = settings.MinimumReplyMessageCount,
            MaximumReplyMessageCount = settings.MaximumReplyMessageCount,
            GroupChatMaximumSpeakersPerTurn =
                settings.GroupChatMaximumSpeakersPerTurn,
            GroupChatWholeGroupMaximumSpeakersPerTurn =
                settings.GroupChatWholeGroupMaximumSpeakersPerTurn,
            GroupChatMaximumMessagesPerTurn =
                settings.GroupChatMaximumMessagesPerTurn
        };
    }
}
