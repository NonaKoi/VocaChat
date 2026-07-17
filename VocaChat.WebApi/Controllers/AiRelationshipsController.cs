using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.Relationships;

namespace VocaChat.WebApi.Controllers;

/// <summary>
/// 提供好友之间有方向关系的查询和保存 API。
/// </summary>
[ApiController]
[Route("api/ai-accounts/{fromAiAccountId}/relationships")]
public sealed class AiRelationshipsController : ControllerBase
{
    private readonly AiRelationshipService _relationshipService;

    public AiRelationshipsController(AiRelationshipService relationshipService)
    {
        _relationshipService = relationshipService;
    }

    /// <summary>
    /// 返回指定好友对其他所有好友的关系。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<AiRelationshipResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<AiRelationshipResponse>> GetAll(
        Guid fromAiAccountId)
    {
        bool succeeded = _relationshipService.TryGetRelationshipsFrom(
            fromAiAccountId,
            out IReadOnlyList<AiRelationship> relationships);

        if (!succeeded)
        {
            return NotFound();
        }

        return Ok(relationships.Select(ToResponse).ToList());
    }

    /// <summary>
    /// 返回两个好友之间指定方向的关系。
    /// </summary>
    [HttpGet("{toAiAccountId}")]
    [ProducesResponseType(
        typeof(AiRelationshipResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<AiRelationshipResponse> Get(
        Guid fromAiAccountId,
        Guid toAiAccountId)
    {
        AiRelationshipOperationStatus status =
            _relationshipService.TryGetRelationship(
                fromAiAccountId,
                toAiAccountId,
                out AiRelationship? relationship);

        return status switch
        {
            AiRelationshipOperationStatus.Success when relationship is not null
                => Ok(ToResponse(relationship)),
            AiRelationshipOperationStatus.SelfRelationshipNotAllowed
                => BadRequest(new { message = "不能查询好友对自己的关系。" }),
            _ => NotFound()
        };
    }

    /// <summary>
    /// 验证并保存两个好友之间指定方向的关系。
    /// </summary>
    [HttpPut("{toAiAccountId}")]
    [ProducesResponseType(
        typeof(AiRelationshipResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<AiRelationshipResponse> Update(
        Guid fromAiAccountId,
        Guid toAiAccountId,
        [FromBody] UpdateAiRelationshipRequest request)
    {
        AiRelationshipOperationStatus status =
            _relationshipService.TryUpdateRelationship(
                fromAiAccountId,
                toAiAccountId,
                request.Familiarity,
                request.Affinity,
                request.Trust,
                out AiRelationship? relationship);

        return status switch
        {
            AiRelationshipOperationStatus.Success when relationship is not null
                => Ok(ToResponse(relationship)),
            AiRelationshipOperationStatus.SelfRelationshipNotAllowed
                => BadRequest(new { message = "不能保存好友对自己的关系。" }),
            AiRelationshipOperationStatus.ValueOutOfRange
                => BadRequest(new { message = "关系数值超出允许范围。" }),
            _ => NotFound()
        };
    }

    private static AiRelationshipResponse ToResponse(
        AiRelationship relationship)
    {
        return new AiRelationshipResponse
        {
            FromAiAccountId = relationship.FromAiAccountId,
            ToAiAccountId = relationship.ToAiAccountId,
            Familiarity = relationship.Familiarity,
            Affinity = relationship.Affinity,
            Trust = relationship.Trust,
            InteractionCount = relationship.InteractionCount,
            LastInteractionAt = relationship.LastInteractionAt,
            UpdatedAt = relationship.UpdatedAt
        };
    }
}
