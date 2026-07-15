using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.GroupChats;

namespace VocaChat.WebApi.Controllers;

/// <summary>
/// 提供群聊查询、创建和添加 AI 成员的 HTTP API。
/// </summary>
[ApiController]
[Route("api/group-chats")]
public class GroupChatsController : ControllerBase
{
    private readonly GroupChatService _groupChatService;

    public GroupChatsController(GroupChatService groupChatService)
    {
        _groupChatService = groupChatService;
    }

    /// <summary>
    /// 返回全部群聊及其 AI 成员摘要。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GroupChatResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<GroupChatResponse>> GetAll()
    {
        IReadOnlyList<GroupChat> groupChats = _groupChatService.GetAllGroupChats();
        List<GroupChatResponse> response = groupChats
            .Select(ToResponse)
            .ToList();

        return Ok(response);
    }

    /// <summary>
    /// 根据 Id 返回群聊及其成员；群聊不存在时返回 404。
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(GroupChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<GroupChatResponse> GetById(Guid id)
    {
        GroupChat? groupChat = _groupChatService.FindById(id);

        if (groupChat is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(groupChat));
    }

    /// <summary>
    /// 使用已有 AI 账号创建群聊；现有创建业务失败统一返回 400。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(GroupChatResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<GroupChatResponse> Create(
        [FromBody] CreateGroupChatRequest request)
    {
        bool succeeded = _groupChatService.TryCreateGroupChat(
            request.Name ?? string.Empty,
            request.MemberAiAccountIds,
            out GroupChat? groupChat,
            out string errorMessage);

        if (!succeeded || groupChat is null)
        {
            return BadRequest(new { message = errorMessage });
        }

        GroupChatResponse response = ToResponse(groupChat);

        return CreatedAtAction(
            nameof(GetById),
            new { id = groupChat.Id },
            response);
    }

    /// <summary>
    /// 向已有群聊添加一个 AI 账号；群聊不存在时返回 404。
    /// </summary>
    [HttpPost("{groupChatId}/members")]
    [ProducesResponseType(typeof(GroupChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<GroupChatResponse> AddMember(
        Guid groupChatId,
        [FromBody] AddGroupChatMemberRequest request)
    {
        GroupChat? groupChat = _groupChatService.FindById(groupChatId);

        if (groupChat is null)
        {
            return NotFound();
        }

        bool succeeded = _groupChatService.TryAddMember(
            groupChat,
            request.AiAccountId,
            out string errorMessage);

        if (!succeeded)
        {
            return BadRequest(new { message = errorMessage });
        }

        return Ok(ToResponse(groupChat));
    }

    /// <summary>
    /// 将内部群聊实体转换为稳定的 HTTP 响应 DTO。
    /// </summary>
    private static GroupChatResponse ToResponse(GroupChat groupChat)
    {
        return new GroupChatResponse
        {
            Id = groupChat.Id,
            Name = groupChat.Name,
            CreatedAt = groupChat.CreatedAt,
            Members = groupChat.Members
                .Select(ToMemberResponse)
                .ToList()
        };
    }

    /// <summary>
    /// 只返回群聊展示所需的 AI 账号 Id 和昵称。
    /// </summary>
    private static GroupChatMemberResponse ToMemberResponse(AiAccount aiAccount)
    {
        return new GroupChatMemberResponse
        {
            Id = aiAccount.Id,
            Nickname = aiAccount.Nickname
        };
    }
}
