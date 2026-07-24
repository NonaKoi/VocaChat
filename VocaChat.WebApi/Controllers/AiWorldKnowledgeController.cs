using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.AiWorldKnowledge;

namespace VocaChat.WebApi.Controllers;

/// <summary>
/// 提供账号世界知识的查询、来源审计和用户治理 API。
/// </summary>
[ApiController]
[Route("api/ai-accounts/{aiAccountId}/world-knowledge")]
public sealed class AiWorldKnowledgeController : ControllerBase
{
    private const int DefaultQueryLimit = 100;
    private readonly AiWorldKnowledgeService _knowledgeService;

    public AiWorldKnowledgeController(
        AiWorldKnowledgeService knowledgeService)
    {
        _knowledgeService = knowledgeService;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<AiWorldKnowledgeResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<IReadOnlyList<AiWorldKnowledgeResponse>> GetAll(
        Guid aiAccountId,
        [FromQuery] Guid? subjectAiAccountId = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = DefaultQueryLimit)
    {
        if (!TryParseOptionalEnum(
                status,
                out AiWorldKnowledgeStatus? parsedStatus))
        {
            return BadRequest(new { message = "世界知识状态无效。" });
        }

        AiWorldKnowledgeOperationStatus queryStatus =
            _knowledgeService.TryGetKnowledgeForManagement(
                aiAccountId,
                subjectAiAccountId,
                parsedStatus,
                limit,
                out IReadOnlyList<AiWorldKnowledge> knowledge,
                out string errorMessage);
        if (queryStatus != AiWorldKnowledgeOperationStatus.Success)
        {
            return ToFailureResult(queryStatus, errorMessage);
        }

        AiWorldKnowledgeOperationStatus countStatus =
            _knowledgeService.TryGetEvidenceCounts(
                aiAccountId,
                knowledge.Select(item => item.Id).ToList(),
                out IReadOnlyDictionary<Guid, int> evidenceCounts,
                out string countError);
        if (countStatus != AiWorldKnowledgeOperationStatus.Success)
        {
            return ToFailureResult(countStatus, countError);
        }

        return Ok(knowledge
            .Select(item => ToResponse(
                item,
                evidenceCounts.GetValueOrDefault(item.Id)))
            .ToList());
    }

    [HttpGet("{knowledgeId}/evidence")]
    [ProducesResponseType(
        typeof(IReadOnlyList<AiWorldKnowledgeEvidenceResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<IReadOnlyList<AiWorldKnowledgeEvidenceResponse>>
        GetEvidence(Guid aiAccountId, Guid knowledgeId)
    {
        AiWorldKnowledgeOperationStatus status =
            _knowledgeService.TryGetEvidenceDetails(
                aiAccountId,
                knowledgeId,
                out IReadOnlyList<AiWorldKnowledgeEvidenceDetails> evidence,
                out string errorMessage);

        return status == AiWorldKnowledgeOperationStatus.Success
            ? Ok(evidence.Select(ToResponse).ToList())
            : ToFailureResult(status, errorMessage);
    }

    [HttpPut("{knowledgeId}")]
    [ProducesResponseType(
        typeof(AiWorldKnowledgeResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<AiWorldKnowledgeResponse> Update(
        Guid aiAccountId,
        Guid knowledgeId,
        [FromBody] UpdateAiWorldKnowledgeRequest request)
    {
        if (!TryParseEnum(
                request.FactNature,
                out AiWorldKnowledgeFactNature factNature)
            || !TryParseEnum(
                request.Mutability,
                out AiWorldKnowledgeMutability mutability))
        {
            return BadRequest(new
            {
                message = "世界知识的事实性质或可变性无效。"
            });
        }

        AiWorldKnowledgeOperationStatus status =
            _knowledgeService.TryUpdateByUser(
                aiAccountId,
                knowledgeId,
                new AiWorldKnowledgeUserUpdateData(
                    request.Summary,
                    factNature,
                    mutability,
                    request.Salience,
                    request.IsUserLocked,
                    request.IsConfirmed),
                out AiWorldKnowledge? knowledge,
                out string errorMessage);

        return status == AiWorldKnowledgeOperationStatus.Success
            && knowledge is not null
                ? Ok(ToResponseWithEvidenceCount(
                    aiAccountId,
                    knowledge))
                : ToFailureResult(status, errorMessage);
    }

    [HttpPut("{knowledgeId}/lock")]
    [ProducesResponseType(
        typeof(AiWorldKnowledgeResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<AiWorldKnowledgeResponse> UpdateLock(
        Guid aiAccountId,
        Guid knowledgeId,
        [FromBody] UpdateAiWorldKnowledgeLockRequest request)
    {
        AiWorldKnowledgeOperationStatus status =
            _knowledgeService.TrySetUserLock(
                aiAccountId,
                knowledgeId,
                request.IsUserLocked,
                out AiWorldKnowledge? knowledge,
                out string errorMessage);

        return status == AiWorldKnowledgeOperationStatus.Success
            && knowledge is not null
                ? Ok(ToResponseWithEvidenceCount(
                    aiAccountId,
                    knowledge))
                : ToFailureResult(status, errorMessage);
    }

    [HttpPut("{knowledgeId}/archive")]
    [ProducesResponseType(
        typeof(AiWorldKnowledgeResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<AiWorldKnowledgeResponse> Archive(
        Guid aiAccountId,
        Guid knowledgeId)
    {
        AiWorldKnowledgeOperationStatus status =
            _knowledgeService.TryArchiveByUser(
                aiAccountId,
                knowledgeId,
                out AiWorldKnowledge? knowledge,
                out string errorMessage);

        return status == AiWorldKnowledgeOperationStatus.Success
            && knowledge is not null
                ? Ok(ToResponseWithEvidenceCount(
                    aiAccountId,
                    knowledge))
                : ToFailureResult(status, errorMessage);
    }

    private ActionResult ToFailureResult(
        AiWorldKnowledgeOperationStatus status,
        string errorMessage)
    {
        return status switch
        {
            AiWorldKnowledgeOperationStatus.AccountNotFound
                or AiWorldKnowledgeOperationStatus.KnowledgeNotFound =>
                    NotFound(),
            AiWorldKnowledgeOperationStatus.PersistenceFailed =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { message = errorMessage }),
            _ => BadRequest(new { message = errorMessage })
        };
    }

    private static AiWorldKnowledgeResponse ToResponse(
        AiWorldKnowledge knowledge,
        int evidenceCount)
    {
        return new AiWorldKnowledgeResponse
        {
            Id = knowledge.Id,
            OwnerAiAccountId = knowledge.OwnerAiAccountId,
            SubjectCharacterWorldId =
                knowledge.SubjectCharacterWorldId,
            SubjectAiAccountId = knowledge.SubjectAiAccountId,
            KnowledgeKey = knowledge.KnowledgeKey,
            Summary = knowledge.Summary,
            FactNature = knowledge.FactNature.ToString(),
            Mutability = knowledge.Mutability.ToString(),
            TrustLevel = knowledge.TrustLevel.ToString(),
            Status = knowledge.Status.ToString(),
            Salience = knowledge.Salience,
            IsUserLocked = knowledge.IsUserLocked,
            EvidenceCount = evidenceCount,
            FirstLearnedAt = knowledge.FirstLearnedAt,
            UpdatedAt = knowledge.UpdatedAt
        };
    }

    /// <summary>
    /// 在知识管理操作完成后补充真实来源数量，避免单条响应与列表查询结果不一致。
    /// </summary>
    private AiWorldKnowledgeResponse ToResponseWithEvidenceCount(
        Guid aiAccountId,
        AiWorldKnowledge knowledge)
    {
        AiWorldKnowledgeOperationStatus status =
            _knowledgeService.TryGetEvidenceCounts(
                aiAccountId,
                [knowledge.Id],
                out IReadOnlyDictionary<Guid, int> evidenceCounts,
                out _);

        int evidenceCount =
            status == AiWorldKnowledgeOperationStatus.Success
                ? evidenceCounts.GetValueOrDefault(knowledge.Id)
                : 0;

        return ToResponse(knowledge, evidenceCount);
    }

    private static AiWorldKnowledgeEvidenceResponse ToResponse(
        AiWorldKnowledgeEvidenceDetails evidence)
    {
        return new AiWorldKnowledgeEvidenceResponse
        {
            EvidenceId = evidence.EvidenceId,
            SourceType = evidence.SourceType.ToString(),
            SourceAiAccountId = evidence.SourceAiAccountId,
            SourceDisplayName = evidence.SourceDisplayName,
            ConversationKind = evidence.ConversationKind,
            ConversationId = evidence.ConversationId,
            ConversationDisplayName =
                evidence.ConversationDisplayName,
            MessageId = evidence.MessageId,
            MessageContent = evidence.MessageContent,
            SentAt = evidence.SentAt,
            EvidenceSummary = evidence.EvidenceSummary
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

    private static bool TryParseOptionalEnum<TEnum>(
        string? value,
        out TEnum? result)
        where TEnum : struct, Enum
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!TryParseEnum(value, out TEnum parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }
}
