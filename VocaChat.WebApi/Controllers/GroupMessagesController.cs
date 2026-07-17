using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.GroupMessages;
using VocaChat.WebApi.Mapping;

namespace VocaChat.WebApi.Controllers;

/// <summary>
/// 提供群消息历史查询和模拟 AI 群聊交互的 HTTP API。
/// </summary>
[ApiController]
[Route("api/group-chats/{groupChatId}/messages")]
public sealed class GroupMessagesController : ControllerBase
{
    private readonly GroupChatService _groupChatService;
    private readonly GroupMessageService _groupMessageService;
    private readonly GroupChatInteractionService _interactionService;

    public GroupMessagesController(
        GroupChatService groupChatService,
        GroupMessageService groupMessageService,
        GroupChatInteractionService interactionService)
    {
        _groupChatService = groupChatService;
        _groupMessageService = groupMessageService;
        _interactionService = interactionService;
    }

    /// <summary>
    /// 返回指定群聊按发送时间和消息 Id 稳定排序的聊天记录。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GroupMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<GroupMessageResponse>> GetAll(
        Guid groupChatId)
    {
        GroupChat? groupChat = _groupChatService.FindById(groupChatId);

        if (groupChat is null)
        {
            return NotFound();
        }

        List<GroupMessageResponse> response = _groupMessageService
            .GetOrderedChatHistory(groupChat)
            .Select(message => ToResponse(message, groupChat))
            .ToList();

        return Ok(response);
    }

    /// <summary>
    /// 保存用户消息，生成并保存一条当前群成员的模拟 AI 回复。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SendGroupMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SendGroupMessageFailureResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(SendGroupMessageFailureResponse), StatusCodes.Status500InternalServerError)]
    public ActionResult<SendGroupMessageResponse> Send(
        Guid groupChatId,
        [FromBody] SendGroupMessageRequest request)
    {
        GroupChat? groupChat = _groupChatService.FindById(groupChatId);

        if (groupChat is null)
        {
            return NotFound();
        }

        GroupChatInteractionResult result = _interactionService
            .ProcessUserMessage(groupChat, request.Content ?? string.Empty);

        if (result.Status == GroupChatInteractionStatus.UserMessageRejected)
        {
            return BadRequest(new SendGroupMessageFailureResponse
            {
                Message = result.ErrorMessage
            });
        }

        if (result.Status == GroupChatInteractionStatus.AiReplyFailed)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new SendGroupMessageFailureResponse
                {
                    Message = result.ErrorMessage,
                    SavedUserMessage = result.UserMessage is null
                        ? null
                        : ToResponse(result.UserMessage, groupChat)
                });
        }

        if (result.UserMessage is null || result.AiReply is null)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new SendGroupMessageFailureResponse
                {
                    Message = "群聊交互完成，但未返回完整的消息结果。"
                });
        }

        return Ok(new SendGroupMessageResponse
        {
            UserMessage = ToResponse(result.UserMessage, groupChat),
            AiReply = ToResponse(result.AiReply, groupChat)
        });
    }

    /// <summary>
    /// 将消息实体映射为稳定的 HTTP 契约，并保留发送时的显示名快照。
    /// </summary>
    private static GroupMessageResponse ToResponse(
        GroupMessage message,
        GroupChat groupChat)
    {
        AiAccount? sender = message.SenderAiAccountId is null
            ? null
            : groupChat.Members.SingleOrDefault(
                member => member.Id == message.SenderAiAccountId.Value);

        return new GroupMessageResponse
        {
            Id = message.Id,
            GroupChatId = message.GroupChatId,
            SenderType = ToSenderTypeText(message.SenderType),
            SenderDisplayName = message.SenderDisplayName,
            SenderAiAccountId = message.SenderAiAccountId,
            SenderAvatarUrl = sender is null
                ? null
                : AiAccountMediaUrls.GetAvatarUrl(sender),
            Content = message.Content,
            SentAt = message.SentAt
        };
    }

    /// <summary>
    /// 使用明确字符串表示发送者类型，避免 HTTP 契约依赖枚举数字。
    /// </summary>
    private static string ToSenderTypeText(MessageSenderType senderType)
    {
        return senderType switch
        {
            MessageSenderType.User => "User",
            MessageSenderType.AiAccount => "AiAccount",
            _ => throw new InvalidOperationException("无法识别群消息发送者类型。")
        };
    }
}
