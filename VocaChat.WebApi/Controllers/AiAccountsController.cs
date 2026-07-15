using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.AiAccounts;

namespace VocaChat.WebApi.Controllers;

/// <summary>
/// 提供 AI 账号查询和创建的 HTTP API。
/// </summary>
[ApiController]
[Route("api/ai-accounts")]
public class AiAccountsController : ControllerBase
{
    private readonly AiAccountService _aiAccountService;

    public AiAccountsController(AiAccountService aiAccountService)
    {
        _aiAccountService = aiAccountService;
    }

    /// <summary>
    /// 返回当前用户已经创建的全部 AI 账号。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AiAccountResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<AiAccountResponse>> GetAll()
    {
        IReadOnlyList<AiAccount> aiAccounts = _aiAccountService.GetAllAccounts();
        List<AiAccountResponse> response = aiAccounts
            .Select(ToResponse)
            .ToList();

        return Ok(response);
    }

    /// <summary>
    /// 根据 Id 返回一个 AI 账号；账号不存在时返回 404。
    /// </summary>
    [HttpGet("{id}", Name = nameof(GetById))]
    [ProducesResponseType(typeof(AiAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<AiAccountResponse> GetById(Guid id)
    {
        AiAccount? aiAccount = _aiAccountService.FindById(id);

        if (aiAccount is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(aiAccount));
    }

    /// <summary>
    /// 验证并创建 AI 账号；所有现有创建业务失败统一返回 400。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AiAccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<AiAccountResponse> Create(
        [FromBody] CreateAiAccountRequest request)
    {
        bool succeeded = _aiAccountService.TryCreateAiAccount(
            request.Nickname ?? string.Empty,
            request.IdentityDescription ?? string.Empty,
            request.Personality ?? string.Empty,
            request.SpeakingStyle ?? string.Empty,
            out AiAccount? aiAccount,
            out string errorMessage);

        if (!succeeded || aiAccount is null)
        {
            return BadRequest(new { message = errorMessage });
        }

        AiAccountResponse response = ToResponse(aiAccount);

        return CreatedAtAction(
            nameof(GetById),
            new { id = aiAccount.Id },
            response);
    }

    /// <summary>
    /// 将内部业务实体转换为稳定的 HTTP 响应 DTO。
    /// </summary>
    private static AiAccountResponse ToResponse(AiAccount aiAccount)
    {
        return new AiAccountResponse
        {
            Id = aiAccount.Id,
            Nickname = aiAccount.Nickname,
            IdentityDescription = aiAccount.IdentityDescription,
            Personality = aiAccount.Personality,
            SpeakingStyle = aiAccount.SpeakingStyle,
            CreatedAt = aiAccount.CreatedAt
        };
    }
}
