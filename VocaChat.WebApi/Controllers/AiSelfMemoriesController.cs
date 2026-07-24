using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.AiSelfMemories;

namespace VocaChat.WebApi.Controllers;

/// <summary>
/// 提供 AI 账号个人记忆的查询和用户管理 API。
/// </summary>
[ApiController]
[Route("api/ai-accounts/{aiAccountId}/self-memories")]
public sealed class AiSelfMemoriesController : ControllerBase
{
    private const int DefaultQueryLimit = 100;
    private readonly AiSelfMemoryService _memoryService;

    public AiSelfMemoriesController(AiSelfMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    /// <summary>
    /// 返回指定账号的个人记忆，可按状态筛选。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<AiSelfMemoryResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<IReadOnlyList<AiSelfMemoryResponse>> GetAll(
        Guid aiAccountId,
        [FromQuery] string? status = null,
        [FromQuery] int limit = DefaultQueryLimit)
    {
        if (!TryParseOptionalStatus(
                status,
                out AiSelfMemoryStatus? parsedStatus))
        {
            return BadRequest(new { message = "个人记忆状态无效。" });
        }

        AiSelfMemoryOperationStatus operationStatus =
            _memoryService.TryGetMemories(
                aiAccountId,
                limit,
                parsedStatus,
                out IReadOnlyList<AiSelfMemory> memories,
                out string errorMessage);

        return operationStatus switch
        {
            AiSelfMemoryOperationStatus.Success => Ok(
                memories.Select(ToResponse).ToList()),
            AiSelfMemoryOperationStatus.AccountNotFound => NotFound(),
            AiSelfMemoryOperationStatus.PersistenceFailed =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { message = errorMessage }),
            _ => BadRequest(new { message = errorMessage })
        };
    }

    /// <summary>
    /// 为指定账号创建一条由本地用户确认的个人记忆。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(
        typeof(AiSelfMemoryResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<AiSelfMemoryResponse> Create(
        Guid aiAccountId,
        [FromBody] CreateAiSelfMemoryRequest request)
    {
        if (!TryParseType(request.Type, out AiSelfMemoryType type))
        {
            return BadRequest(new { message = "个人记忆类型无效。" });
        }
        if (!TryParseOptionalFactNature(
                request.FactNature,
                out AiSelfMemoryFactNature? factNature)
            || !TryParseOptionalMutability(
                request.Mutability,
                out AiSelfMemoryMutability? mutability))
        {
            return BadRequest(new { message = "个人记忆的事实性质或可变性无效。" });
        }

        AiSelfMemoryOperationStatus status =
            _memoryService.TryCreateUserMemory(
                aiAccountId,
                new AiSelfMemoryWriteData(
                    type,
                    request.Summary,
                    request.Salience,
                    request.IsUserLocked,
                    request.OccurredAt,
                    request.ValidFrom,
                    request.ValidUntil,
                    request.FactKey,
                    factNature,
                    mutability,
                    request.CharacterWorldId),
                out AiSelfMemory? memory,
                out string errorMessage);

        if (status == AiSelfMemoryOperationStatus.Success
            && memory is not null)
        {
            return CreatedAtAction(
                nameof(GetAll),
                new { aiAccountId },
                ToResponse(memory));
        }

        return ToFailureResult(status, errorMessage);
    }

    /// <summary>
    /// 修改一条属于指定账号的个人记忆。
    /// </summary>
    [HttpPut("{memoryId}")]
    [ProducesResponseType(
        typeof(AiSelfMemoryResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<AiSelfMemoryResponse> Update(
        Guid aiAccountId,
        Guid memoryId,
        [FromBody] UpdateAiSelfMemoryRequest request)
    {
        if (!TryParseType(request.Type, out AiSelfMemoryType type))
        {
            return BadRequest(new { message = "个人记忆类型无效。" });
        }
        if (!TryParseOptionalFactNature(
                request.FactNature,
                out AiSelfMemoryFactNature? factNature)
            || !TryParseOptionalMutability(
                request.Mutability,
                out AiSelfMemoryMutability? mutability))
        {
            return BadRequest(new { message = "个人记忆的事实性质或可变性无效。" });
        }

        AiSelfMemoryOperationStatus status =
            _memoryService.TryUpdateUserMemory(
                aiAccountId,
                memoryId,
                new AiSelfMemoryWriteData(
                    type,
                    request.Summary,
                    request.Salience,
                    request.IsUserLocked,
                    request.OccurredAt,
                    request.ValidFrom,
                    request.ValidUntil,
                    request.FactKey,
                    factNature,
                    mutability,
                    request.CharacterWorldId),
                out AiSelfMemory? memory,
                out string errorMessage);

        return status == AiSelfMemoryOperationStatus.Success
            && memory is not null
                ? Ok(ToResponse(memory))
                : ToFailureResult(status, errorMessage);
    }

    /// <summary>
    /// 将一条个人记忆归档或恢复为有效状态。
    /// </summary>
    [HttpPut("{memoryId}/status")]
    [ProducesResponseType(
        typeof(AiSelfMemoryResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<AiSelfMemoryResponse> UpdateStatus(
        Guid aiAccountId,
        Guid memoryId,
        [FromBody] UpdateAiSelfMemoryStatusRequest request)
    {
        if (!Enum.TryParse(
                request.Status,
                ignoreCase: true,
                out AiSelfMemoryStatus parsedStatus)
            || !Enum.IsDefined(parsedStatus))
        {
            return BadRequest(new { message = "个人记忆状态无效。" });
        }

        AiSelfMemoryOperationStatus status =
            _memoryService.TryChangeUserManagedStatus(
                aiAccountId,
                memoryId,
                parsedStatus,
                out AiSelfMemory? memory,
                out string errorMessage);

        return status == AiSelfMemoryOperationStatus.Success
            && memory is not null
                ? Ok(ToResponse(memory))
                : ToFailureResult(status, errorMessage);
    }

    private ActionResult<AiSelfMemoryResponse> ToFailureResult(
        AiSelfMemoryOperationStatus status,
        string errorMessage)
    {
        return status switch
        {
            AiSelfMemoryOperationStatus.AccountNotFound
                or AiSelfMemoryOperationStatus.MemoryNotFound => NotFound(),
            AiSelfMemoryOperationStatus.AlreadyExists => Conflict(
                new { message = errorMessage }),
            AiSelfMemoryOperationStatus.PersistenceFailed => StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = errorMessage }),
            _ => BadRequest(new { message = errorMessage })
        };
    }

    private static bool TryParseType(
        string value,
        out AiSelfMemoryType type)
    {
        return Enum.TryParse(value, ignoreCase: true, out type)
            && Enum.IsDefined(type);
    }

    private static bool TryParseOptionalStatus(
        string? value,
        out AiSelfMemoryStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!Enum.TryParse(
                value,
                ignoreCase: true,
                out AiSelfMemoryStatus parsedStatus)
            || !Enum.IsDefined(parsedStatus))
        {
            return false;
        }

        status = parsedStatus;
        return true;
    }

    private static bool TryParseOptionalFactNature(
        string? value,
        out AiSelfMemoryFactNature? factNature)
    {
        factNature = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!Enum.TryParse(
                value,
                ignoreCase: true,
                out AiSelfMemoryFactNature parsedValue)
            || !Enum.IsDefined(parsedValue))
        {
            return false;
        }

        factNature = parsedValue;
        return true;
    }

    private static bool TryParseOptionalMutability(
        string? value,
        out AiSelfMemoryMutability? mutability)
    {
        mutability = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!Enum.TryParse(
                value,
                ignoreCase: true,
                out AiSelfMemoryMutability parsedValue)
            || !Enum.IsDefined(parsedValue))
        {
            return false;
        }

        mutability = parsedValue;
        return true;
    }

    private static AiSelfMemoryResponse ToResponse(AiSelfMemory memory)
    {
        return new AiSelfMemoryResponse
        {
            Id = memory.Id,
            AiAccountId = memory.AiAccountId,
            Type = memory.Type.ToString(),
            Summary = memory.Summary,
            FactKey = memory.FactKey,
            FactNature = memory.FactNature.ToString(),
            Mutability = memory.Mutability.ToString(),
            TrustLevel = memory.TrustLevel.ToString(),
            CharacterWorldId = memory.CharacterWorldId,
            Source = memory.Source.ToString(),
            Status = memory.Status.ToString(),
            Salience = memory.Salience,
            IsUserLocked = memory.IsUserLocked,
            SourceConversationId = memory.SourceConversationId,
            SourceMessageId = memory.SourceMessageId,
            SupersedesMemoryId = memory.SupersedesMemoryId,
            OccurredAt = memory.OccurredAt,
            ValidFrom = memory.ValidFrom,
            ValidUntil = memory.ValidUntil,
            CreatedAt = memory.CreatedAt,
            UpdatedAt = memory.UpdatedAt
        };
    }
}
