using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.PrivateChats;
using VocaChat.WebApi.Mapping;

namespace VocaChat.WebApi.Controllers;

/// <summary>提供私聊资料、消息历史和模拟 AI 回复 API。</summary>
[ApiController]
[Route("api/private-chats")]
public sealed class PrivateChatsController : ControllerBase
{
    private readonly PrivateChatService _privateChatService;
    private readonly PrivateChatInteractionService _interactionService;

    public PrivateChatsController(
        PrivateChatService privateChatService,
        PrivateChatInteractionService interactionService)
    {
        _privateChatService = privateChatService;
        _interactionService = interactionService;
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PrivateChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PrivateChatResponse> GetById(Guid id)
    {
        PrivateChat? privateChat = _privateChatService.FindById(id);
        return privateChat is null
            ? NotFound()
            : Ok(PrivateChatResponseMapper.ToResponse(privateChat));
    }

    [HttpGet("{id}/messages")]
    [ProducesResponseType(typeof(IReadOnlyList<PrivateMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<PrivateMessageResponse>> GetMessages(
        Guid id)
    {
        PrivateChat? privateChat = _privateChatService.FindById(id);

        if (privateChat is null)
        {
            return NotFound();
        }

        AiAccount friend = privateChat.Contact.AiAccount;
        return Ok(_privateChatService.GetOrderedChatHistory(id)
            .Select(message =>
                PrivateChatResponseMapper.ToMessageResponse(message, friend))
            .ToList());
    }

    [HttpPost("{id}/messages")]
    [ProducesResponseType(typeof(SendPrivateMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SendPrivateMessageFailureResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(SendPrivateMessageFailureResponse), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<SendPrivateMessageResponse> SendMessage(
        Guid id,
        [FromBody] SendPrivateMessageRequest request)
    {
        PrivateChat? privateChat = _privateChatService.FindById(id);

        if (privateChat is null)
        {
            return NotFound();
        }

        PrivateChatInteractionResult result =
            _interactionService.ProcessUserMessage(
                privateChat,
                request.Content);
        AiAccount friend = privateChat.Contact.AiAccount;

        if (result.Status == PrivateChatInteractionStatus.UserMessageRejected)
        {
            return BadRequest(new SendPrivateMessageFailureResponse
            {
                Message = result.ErrorMessage
            });
        }

        if (result.Status == PrivateChatInteractionStatus.AiReplyFailed)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new SendPrivateMessageFailureResponse
                {
                    Message = result.ErrorMessage,
                    SavedUserMessage = PrivateChatResponseMapper
                        .ToMessageResponse(result.UserMessage!, friend)
                });
        }

        return Ok(new SendPrivateMessageResponse
        {
            UserMessage = PrivateChatResponseMapper.ToMessageResponse(
                result.UserMessage!,
                friend),
            AiReply = PrivateChatResponseMapper.ToMessageResponse(
                result.AiReply!,
                friend)
        });
    }
}
