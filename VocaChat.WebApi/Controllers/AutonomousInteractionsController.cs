using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.AutonomousInteractions;

namespace VocaChat.WebApi.Controllers;

/// <summary>
/// 提供不创建会话或消息的好友自主互动判断预览 API。
/// </summary>
[ApiController]
[Route("api/autonomous-interactions")]
public sealed class AutonomousInteractionsController : ControllerBase
{
    private readonly AutonomousPrivateChatJudge _privateChatJudge;

    public AutonomousInteractionsController(
        AutonomousPrivateChatJudge privateChatJudge)
    {
        _privateChatJudge = privateChatJudge;
    }

    /// <summary>
    /// 评估两个好友当前是否适合发起自主私信。
    /// </summary>
    [HttpPost("private-chat/evaluate")]
    [ProducesResponseType(
        typeof(AutonomousPrivateChatDecisionResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<AutonomousPrivateChatDecisionResponse>
        EvaluatePrivateChat(
            [FromBody] EvaluateAutonomousPrivateChatRequest request)
    {
        double randomJitter = Random.Shared.NextDouble() * 20 - 10;
        AutonomousPrivateChatDecision decision = _privateChatJudge.Evaluate(
            request.FirstAiAccountId,
            request.SecondAiAccountId,
            DateTime.Now,
            randomJitter);

        return decision.Stage switch
        {
            AutonomousPrivateChatDecisionStage.SelfInteractionNotAllowed
                => BadRequest(new { message = "不能判断好友与自己的互动。" }),
            AutonomousPrivateChatDecisionStage.AccountNotFound
                => NotFound(),
            _ => Ok(ToResponse(decision))
        };
    }

    private static AutonomousPrivateChatDecisionResponse ToResponse(
        AutonomousPrivateChatDecision decision)
    {
        return new AutonomousPrivateChatDecisionResponse
        {
            IsApproved = decision.IsApproved,
            Stage = decision.Stage.ToString(),
            FirstAiAccountId = decision.FirstAiAccountId,
            SecondAiAccountId = decision.SecondAiAccountId,
            InitiatorAiAccountId = decision.InitiatorAiAccountId,
            RecipientAiAccountId = decision.RecipientAiAccountId,
            RelationshipScore = decision.RelationshipScore,
            InitiativeAdjustment = decision.InitiativeAdjustment,
            RandomJitter = decision.RandomJitter,
            FinalScore = decision.FinalScore,
            Threshold = decision.Threshold,
            CooldownEndsAt = decision.CooldownEndsAt
        };
    }
}
