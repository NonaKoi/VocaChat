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
            .Select(message => GroupMessageResponseMapper.ToResponse(
                message,
                groupChat.Members))
            .ToList();

        return Ok(response);
    }

    /// <summary>
    /// 保存用户消息，并按本轮发言计划生成和保存一至两条群成员回复。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SendGroupMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SendGroupMessageFailureResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(SendGroupMessageFailureResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SendGroupMessageResponse>> Send(
        Guid groupChatId,
        [FromBody] SendGroupMessageRequest request,
        CancellationToken cancellationToken)
    {
        GroupChat? groupChat = _groupChatService.FindById(groupChatId);

        if (groupChat is null)
        {
            return NotFound();
        }

        GroupChatInteractionResult result = await _interactionService
            .ProcessUserMessageAsync(
                groupChat,
                request.Content ?? string.Empty,
                cancellationToken);

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
                        : GroupMessageResponseMapper.ToResponse(
                            result.UserMessage,
                            groupChat.Members),
                    SavedAiReplies = result.AiReplies
                        .Select(reply => GroupMessageResponseMapper.ToResponse(
                            reply,
                            groupChat.Members))
                        .ToList()
                });
        }

        if (result.UserMessage is null || result.AiReplies.Count == 0)
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
            UserMessage = GroupMessageResponseMapper.ToResponse(
                result.UserMessage,
                groupChat.Members),
            AiReplies = result.AiReplies
                .Select(reply => GroupMessageResponseMapper.ToResponse(
                    reply,
                    groupChat.Members))
                .ToList(),
            ReplyCompletion = result.Status
                == GroupChatInteractionStatus.PartiallySucceeded
                    ? "Partial"
                    : "Complete",
            WarningMessage = result.Status
                == GroupChatInteractionStatus.PartiallySucceeded
                    ? result.ErrorMessage
                    : null
        });
    }

}
