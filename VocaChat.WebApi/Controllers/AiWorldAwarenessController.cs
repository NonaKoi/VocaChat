using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.AiWorldKnowledge;
using VocaChat.WebApi.Mapping;

namespace VocaChat.WebApi.Controllers;

/// <summary>
/// 提供账号的平行世界认知、方向性世界认知和派生熟悉度管理 API。
/// </summary>
[ApiController]
[Route("api/ai-accounts/{aiAccountId}/world-awareness")]
public sealed class AiWorldAwarenessController : ControllerBase
{
    private readonly AiAccountService _aiAccountService;
    private readonly AiWorldAwarenessService _awarenessService;

    public AiWorldAwarenessController(
        AiAccountService aiAccountService,
        AiWorldAwarenessService awarenessService)
    {
        _aiAccountService = aiAccountService;
        _awarenessService = awarenessService;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(AiWorldAwarenessOverviewResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<AiWorldAwarenessOverviewResponse> GetOverview(
        Guid aiAccountId)
    {
        if (_aiAccountService.FindById(aiAccountId) is null)
        {
            return NotFound();
        }

        AiWorldAwarenessOperationStatus parallelStatus =
            _awarenessService.TryGetParallelWorldAwareness(
                aiAccountId,
                out AiParallelWorldAwarenessState parallelState,
                out AiParallelWorldAwareness? parallel,
                out string parallelError);
        if (parallelStatus != AiWorldAwarenessOperationStatus.Success)
        {
            return ToFailureResult(parallelStatus, parallelError);
        }

        List<WorldAwarenessSubjectResponse> subjects = new();
        foreach (AiAccount subject in _aiAccountService
                     .GetAllAccounts()
                     .Where(account => account.Id != aiAccountId))
        {
            ActionResult<WorldAwarenessSubjectResponse> subjectResult =
                BuildSubjectResponse(aiAccountId, subject);
            if (subjectResult.Result is not null)
            {
                return new ActionResult<AiWorldAwarenessOverviewResponse>(
                    subjectResult.Result);
            }

            subjects.Add(subjectResult.Value!);
        }

        return Ok(new AiWorldAwarenessOverviewResponse
        {
            AiAccountId = aiAccountId,
            ParallelWorld = ToResponse(parallelState, parallel),
            Subjects = subjects
                .OrderBy(subject => subject.Nickname)
                .ThenBy(subject => subject.AiAccountId)
                .ToList()
        });
    }

    [HttpPut("parallel")]
    [ProducesResponseType(
        typeof(ParallelWorldAwarenessResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<ParallelWorldAwarenessResponse> UpdateParallel(
        Guid aiAccountId,
        [FromBody] UpdateAiWorldAwarenessRequest request)
    {
        if (!TryParseEnum(
                request.State,
                out AiParallelWorldAwarenessState state))
        {
            return BadRequest(new
            {
                message = "平行世界认知状态无效。"
            });
        }

        AiWorldAwarenessOperationStatus status =
            _awarenessService.TrySetParallelWorldAwarenessByUser(
                aiAccountId,
                state,
                request.IsUserLocked,
                out AiParallelWorldAwareness? awareness,
                out string errorMessage);

        return status == AiWorldAwarenessOperationStatus.Success
            ? Ok(ToResponse(state, awareness))
            : ToFailureResult(status, errorMessage);
    }

    [HttpPut("subjects/{subjectAiAccountId}")]
    [ProducesResponseType(
        typeof(WorldAwarenessSubjectResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<WorldAwarenessSubjectResponse> UpdateSubject(
        Guid aiAccountId,
        Guid subjectAiAccountId,
        [FromBody] UpdateAiWorldAwarenessRequest request)
    {
        if (!TryParseEnum(
                request.State,
                out AiWorldAwarenessState state))
        {
            return BadRequest(new
            {
                message = "好友世界认知状态无效。"
            });
        }

        AiWorldAwarenessOperationStatus status =
            _awarenessService.TrySetWorldAwarenessByUser(
                aiAccountId,
                subjectAiAccountId,
                state,
                request.IsUserLocked,
                out _,
                out string errorMessage);
        if (status != AiWorldAwarenessOperationStatus.Success)
        {
            return ToFailureResult(status, errorMessage);
        }

        AiAccount? subject =
            _aiAccountService.FindById(subjectAiAccountId);
        return subject is null
            ? NotFound()
            : BuildSubjectResponse(aiAccountId, subject);
    }

    private ActionResult<WorldAwarenessSubjectResponse>
        BuildSubjectResponse(
            Guid observerAiAccountId,
            AiAccount subject)
    {
        AiWorldAwarenessOperationStatus awarenessStatus =
            _awarenessService.TryGetWorldAwareness(
                observerAiAccountId,
                subject.Id,
                out AiWorldAwarenessState state,
                out AiWorldAwareness? awareness,
                out string awarenessError);
        if (awarenessStatus != AiWorldAwarenessOperationStatus.Success)
        {
            return ToFailureResult(awarenessStatus, awarenessError);
        }

        AiWorldAwarenessOperationStatus familiarityStatus =
            _awarenessService.TryGetFamiliarity(
                observerAiAccountId,
                subject.Id,
                out AiWorldFamiliarity familiarity,
                out string familiarityError);
        if (familiarityStatus != AiWorldAwarenessOperationStatus.Success)
        {
            return ToFailureResult(
                familiarityStatus,
                familiarityError);
        }

        return new WorldAwarenessSubjectResponse
        {
            AiAccountId = subject.Id,
            Nickname = subject.Nickname,
            AvatarUrl = AiAccountMediaUrls.GetAvatarUrl(subject),
            CharacterWorldId = subject.CharacterWorldId,
            CharacterWorldName =
                subject.CharacterWorld?.Name ?? string.Empty,
            AwarenessState = state.ToString(),
            IsUserLocked = awareness?.IsUserLocked ?? false,
            AwarenessEvidenceCount = awareness?.EvidenceCount ?? 0,
            AwarenessConversationCount =
                awareness?.DistinctConversationCount ?? 0,
            FamiliarityLevel = familiarity.Level.ToString(),
            ActiveKnowledgeCount = familiarity.ActiveKnowledgeCount,
            DistinctTopicCount = familiarity.DistinctTopicCount,
            KnowledgeEvidenceCount = familiarity.EvidenceCount,
            KnowledgeConversationCount =
                familiarity.DistinctConversationCount
        };
    }

    private ActionResult ToFailureResult(
        AiWorldAwarenessOperationStatus status,
        string errorMessage)
    {
        return status switch
        {
            AiWorldAwarenessOperationStatus.AccountNotFound => NotFound(),
            AiWorldAwarenessOperationStatus.PersistenceFailed =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { message = errorMessage }),
            _ => BadRequest(new { message = errorMessage })
        };
    }

    private static ParallelWorldAwarenessResponse ToResponse(
        AiParallelWorldAwarenessState state,
        AiParallelWorldAwareness? awareness)
    {
        return new ParallelWorldAwarenessResponse
        {
            State = state.ToString(),
            IsUserLocked = awareness?.IsUserLocked ?? false,
            FirstInformedAt = awareness?.FirstInformedAt,
            AcceptedAt = awareness?.AcceptedAt,
            UpdatedAt = awareness?.UpdatedAt
        };
    }

    private static bool TryParseEnum<TEnum>(
        string value,
        out TEnum result)
        where TEnum : struct, Enum
    {
        return Enum.TryParse(value, ignoreCase: true, out result)
            && Enum.IsDefined(result);
    }
}
