using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.AutonomousInteractions;
using VocaChat.WebApi.Mapping;

namespace VocaChat.WebApi.Controllers;

/// <summary>
/// 提供好友自主互动的只读判断预览和单次受控执行 API。
/// </summary>
[ApiController]
[Route("api/autonomous-interactions")]
public sealed class AutonomousInteractionsController : ControllerBase
{
    private readonly AutonomousPrivateChatJudge _privateChatJudge;
    private readonly AutonomousPrivateChatExecutionService _executionService;
    private readonly PrivateChatService _privateChatService;

    public AutonomousInteractionsController(
        AutonomousPrivateChatJudge privateChatJudge,
        AutonomousPrivateChatExecutionService executionService,
        PrivateChatService privateChatService)
    {
        _privateChatJudge = privateChatJudge;
        _executionService = executionService;
        _privateChatService = privateChatService;
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

    /// <summary>
    /// 对指定好友执行一次判断；只有判断通过时才创建或复用私信并保存一轮交流。
    /// </summary>
    [HttpPost("private-chat/run")]
    [ProducesResponseType(
        typeof(AutonomousPrivateChatExecutionResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(AutonomousPrivateChatExecutionResponse),
        StatusCodes.Status500InternalServerError)]
    public ActionResult<AutonomousPrivateChatExecutionResponse>
        RunPrivateChat(
            [FromBody] RunAutonomousPrivateChatRequest request)
    {
        double randomJitter = Random.Shared.NextDouble() * 20 - 10;
        AutonomousPrivateChatExecutionResult result =
            _executionService.Execute(
                request.FirstAiAccountId,
                request.SecondAiAccountId,
                DateTime.Now,
                randomJitter);

        if (result.Decision.Stage
            == AutonomousPrivateChatDecisionStage.SelfInteractionNotAllowed)
        {
            return BadRequest(new { message = "不能让好友与自己发起私信。" });
        }

        if (result.Decision.Stage
            == AutonomousPrivateChatDecisionStage.AccountNotFound)
        {
            return NotFound();
        }

        AutonomousPrivateChatExecutionResponse response = ToResponse(result);

        return result.Status is AutonomousPrivateChatExecutionStatus.Completed
            or AutonomousPrivateChatExecutionStatus.DecisionRejected
                ? Ok(response)
                : StatusCode(
                    StatusCodes.Status500InternalServerError,
                    response);
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

    private AutonomousPrivateChatExecutionResponse ToResponse(
        AutonomousPrivateChatExecutionResult result)
    {
        IReadOnlyList<AiAccount> participants = result.PrivateChat is null
            ? Array.Empty<AiAccount>()
            : _privateChatService.GetAiParticipants(result.PrivateChat);

        return new AutonomousPrivateChatExecutionResponse
        {
            Status = result.Status.ToString(),
            Decision = ToResponse(result.Decision),
            PrivateChat = result.PrivateChat is null
                ? null
                : PrivateChatResponseMapper.ToResponse(result.PrivateChat),
            PrivateChatCreated = result.PrivateChatCreated,
            InitiatorMessage = result.InitiatorMessage is null
                ? null
                : PrivateChatResponseMapper.ToMessageResponse(
                    result.InitiatorMessage,
                    participants),
            RecipientReply = result.RecipientReply is null
                ? null
                : PrivateChatResponseMapper.ToMessageResponse(
                    result.RecipientReply,
                    participants),
            ErrorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? null
                : result.ErrorMessage
        };
    }
}
