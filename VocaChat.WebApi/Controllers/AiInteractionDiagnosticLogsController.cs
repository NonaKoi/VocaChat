using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.Settings;

namespace VocaChat.WebApi.Controllers;

/// <summary>提供设置页使用的 AI 互动诊断日志。</summary>
[ApiController]
[Route("api/settings/interaction-logs")]
public sealed class AiInteractionDiagnosticLogsController : ControllerBase
{
    private readonly AiInteractionDiagnosticLogService _logService;

    public AiInteractionDiagnosticLogsController(
        AiInteractionDiagnosticLogService logService)
    {
        _logService = logService;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<AiInteractionDiagnosticLogResponse>),
        StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<AiInteractionDiagnosticLogResponse>> Get(
        [FromQuery] int limit = 100)
    {
        return Ok(_logService.GetRecent(limit).Select(ToResponse).ToList());
    }

    private static AiInteractionDiagnosticLogResponse ToResponse(
        AiInteractionDiagnosticLog log) =>
        new()
        {
            Id = log.Id,
            OccurredAt = log.OccurredAt,
            Severity = log.Severity.ToString(),
            Code = log.Code.ToString(),
            Scenario = log.Scenario,
            AiAccountId = log.AiAccountId,
            ConversationId = log.ConversationId,
            Summary = log.Summary,
            Detail = log.Detail,
            WasRecovered = log.WasRecovered
        };
}

