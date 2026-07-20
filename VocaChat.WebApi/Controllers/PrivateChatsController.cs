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
    private readonly AiInteractionDiagnosticLogService _diagnosticLogService;

    public PrivateChatsController(
        PrivateChatService privateChatService,
        PrivateChatInteractionService interactionService,
        AiInteractionDiagnosticLogService diagnosticLogService)
    {
        _privateChatService = privateChatService;
        _interactionService = interactionService;
        _diagnosticLogService = diagnosticLogService;
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

        IReadOnlyList<AiAccount> participants =
            _privateChatService.GetAiParticipants(privateChat);
        return Ok(_privateChatService.GetOrderedChatHistory(id)
            .Select(message =>
                PrivateChatResponseMapper.ToMessageResponse(
                    message,
                    participants))
            .ToList());
    }

    [HttpPost("{id}/messages")]
    [ProducesResponseType(typeof(SendPrivateMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SendPrivateMessageFailureResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(SendPrivateMessageFailureResponse), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SendPrivateMessageResponse>> SendMessage(
        Guid id,
        [FromBody] SendPrivateMessageRequest request,
        CancellationToken cancellationToken)
    {
        PrivateChat? privateChat = _privateChatService.FindById(id);

        if (privateChat is null)
        {
            return NotFound();
        }

        PrivateChatInteractionResult result =
            await _interactionService.ProcessUserMessageAsync(
                privateChat,
                request.Content,
                request.ClientMessageId,
                cancellationToken);
        IReadOnlyList<AiAccount> participants =
            _privateChatService.GetAiParticipants(privateChat);

        if (result.Status == PrivateChatInteractionStatus.UserMessageRejected)
        {
            return BadRequest(new SendPrivateMessageFailureResponse
            {
                Message = result.ErrorMessage
            });
        }

        if (result.Status == PrivateChatInteractionStatus.AiReplyFailed)
        {
            _diagnosticLogService.TryRecord(
                AiInteractionDiagnosticSeverity.Error,
                AiInteractionDiagnosticCode.MessageGenerationFailed,
                AiMessageGenerationScenario.UserPrivateChat,
                privateChat.Contact?.AiAccountId,
                privateChat.Id,
                "好友回复生成失败，用户消息已经保存。",
                result.ErrorMessage);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new SendPrivateMessageFailureResponse
                {
                    Message = "好友回复暂时生成失败，已保留你发送的消息。",
                    SavedUserMessage = PrivateChatResponseMapper
                        .ToMessageResponse(result.UserMessage!, participants),
                    SavedAiReplies = result.AiReplies
                        .Select(reply => PrivateChatResponseMapper
                            .ToMessageResponse(reply, participants))
                        .ToList()
                });
        }

        if (result.Status == PrivateChatInteractionStatus.PartiallySucceeded)
        {
            _diagnosticLogService.TryRecord(
                AiInteractionDiagnosticSeverity.Warning,
                AiInteractionDiagnosticCode.MessageGenerationFailed,
                AiMessageGenerationScenario.UserPrivateChat,
                privateChat.Contact?.AiAccountId,
                privateChat.Id,
                "好友只完成了部分回复。",
                result.ErrorMessage);
        }

        return Ok(new SendPrivateMessageResponse
        {
            UserMessage = PrivateChatResponseMapper.ToMessageResponse(
                result.UserMessage!,
                participants),
            AiReplies = result.AiReplies
                .Select(reply => PrivateChatResponseMapper.ToMessageResponse(
                    reply,
                    participants))
                .ToList(),
            ReplyCompletion = result.Status
                == PrivateChatInteractionStatus.PartiallySucceeded
                    ? "Partial"
                    : "Complete",
            WarningMessage = result.Status
                == PrivateChatInteractionStatus.PartiallySucceeded
                    ? "好友只完成了部分回复，详细信息已记录到互动日志。"
                    : null
        });
    }
}
