using Microsoft.AspNetCore.Mvc;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.Conversations;
using VocaChat.WebApi.Mapping;

namespace VocaChat.WebApi.Controllers;

/// <summary>提供私聊和群聊合并后的最近会话摘要。</summary>
[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController : ControllerBase
{
    private readonly ConversationService _conversationService;

    public ConversationsController(ConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationSummaryResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ConversationSummaryResponse>> GetAll()
    {
        return Ok(_conversationService.GetRecentConversations()
            .Select(summary => new ConversationSummaryResponse
            {
                Kind = summary.Kind.ToString(),
                Category = summary.Category.ToString(),
                Id = summary.Id,
                ContactId = summary.ContactId,
                DisplayName = summary.DisplayName,
                AvatarUrl = summary.AvatarAiAccountId is Guid accountId
                    ? AiAccountMediaUrls.GetAvatarUrl(
                        accountId,
                        summary.AvatarMediaId)
                    : null,
                MemberCount = summary.MemberCount,
                LatestSenderDisplayName = summary.LatestSenderDisplayName,
                LatestMessageContent = summary.LatestMessageContent,
                LatestMessageAt = summary.LatestMessageAt,
                CreatedAt = summary.CreatedAt
            })
            .ToList());
    }
}
