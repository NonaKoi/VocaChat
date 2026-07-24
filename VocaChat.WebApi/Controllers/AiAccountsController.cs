using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.AiAccounts;
using VocaChat.WebApi.Mapping;
using VocaChat.WebApi.Services;

namespace VocaChat.WebApi.Controllers;

/// <summary>
/// 提供 AI 账号查询和创建的 HTTP API。
/// </summary>
[ApiController]
[Route("api/ai-accounts")]
public class AiAccountsController : ControllerBase
{
    private readonly AiAccountService _aiAccountService;
    private readonly AiAccountMediaService _aiAccountMediaService;

    public AiAccountsController(
        AiAccountService aiAccountService,
        AiAccountMediaService aiAccountMediaService)
    {
        _aiAccountService = aiAccountService;
        _aiAccountMediaService = aiAccountMediaService;
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
            .Select(AiAccountResponseMapper.ToResponse)
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

        return Ok(AiAccountResponseMapper.ToResponse(aiAccount));
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
        if (!TryParseProfileEnum(
                request.Gender,
                AiAccountGender.Unspecified,
                out AiAccountGender gender))
        {
            return BadRequest(new { message = "性别值无效。" });
        }

        if (!TryParseProfileEnum(
                request.OnlineStatus,
                OnlineStatus.Offline,
                out OnlineStatus onlineStatus))
        {
            return BadRequest(new { message = "在线状态值无效。" });
        }

        bool succeeded = _aiAccountService.TryCreateAiAccount(
            new AiAccountCreationData
            {
                Nickname = request.Nickname ?? string.Empty,
                VcNumber = request.VcNumber,
                IdentityDescription = request.IdentityDescription ?? string.Empty,
                Personality = request.Personality ?? string.Empty,
                SpeakingStyle = request.SpeakingStyle ?? string.Empty,
                Signature = request.Signature ?? string.Empty,
                Birthday = request.Birthday,
                Gender = gender,
                Location = request.Location ?? string.Empty,
                Occupation = request.Occupation ?? string.Empty,
                Hometown = request.Hometown ?? string.Empty,
                OnlineStatus = onlineStatus,
                CharacterWorldId = request.CharacterWorldId,
                InterestTags = request.InterestTags ?? Array.Empty<string>(),
                PersonalityTags = request.PersonalityTags ?? Array.Empty<string>()
            },
            out AiAccount? aiAccount,
            out string errorMessage);

        if (!succeeded || aiAccount is null)
        {
            return BadRequest(new { message = errorMessage });
        }

        AiAccountResponse response = AiAccountResponseMapper.ToResponse(aiAccount);

        return CreatedAtAction(
            nameof(GetById),
            new { id = aiAccount.Id },
            response);
    }

    /// <summary>
    /// 更新指定 AI 账号的完整档案；系统主键、创建时间和媒体保持不变。
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AiAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<AiAccountResponse> Update(
        Guid id,
        [FromBody] UpdateAiAccountRequest request)
    {
        if (!TryParseProfileEnum(
                request.Gender,
                AiAccountGender.Unspecified,
                out AiAccountGender gender))
        {
            return BadRequest(new { message = "性别值无效。" });
        }

        if (!TryParseProfileEnum(
                request.OnlineStatus,
                OnlineStatus.Offline,
                out OnlineStatus onlineStatus))
        {
            return BadRequest(new { message = "在线状态值无效。" });
        }

        AiAccountUpdateStatus status = _aiAccountService.TryUpdateAiAccount(
            id,
            new AiAccountUpdateData
            {
                Nickname = request.Nickname ?? string.Empty,
                VcNumber = request.VcNumber ?? string.Empty,
                IdentityDescription =
                    request.IdentityDescription ?? string.Empty,
                Personality = request.Personality ?? string.Empty,
                SpeakingStyle = request.SpeakingStyle ?? string.Empty,
                Signature = request.Signature ?? string.Empty,
                Birthday = request.Birthday,
                Gender = gender,
                Location = request.Location ?? string.Empty,
                Occupation = request.Occupation ?? string.Empty,
                Hometown = request.Hometown ?? string.Empty,
                OnlineStatus = onlineStatus,
                CharacterWorldId = request.CharacterWorldId,
                InterestTags = request.InterestTags ?? Array.Empty<string>(),
                PersonalityTags =
                    request.PersonalityTags ?? Array.Empty<string>()
            },
            out AiAccount? aiAccount,
            out string errorMessage);

        if (status == AiAccountUpdateStatus.AccountNotFound)
        {
            return NotFound();
        }

        if (status == AiAccountUpdateStatus.PersistenceFailed)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = errorMessage });
        }

        if (status == AiAccountUpdateStatus.CharacterWorldNotFound)
        {
            return BadRequest(new { message = errorMessage });
        }

        if (status != AiAccountUpdateStatus.Success || aiAccount is null)
        {
            return BadRequest(new { message = errorMessage });
        }

        return Ok(AiAccountResponseMapper.ToResponse(aiAccount));
    }

    /// <summary>
    /// 使用 multipart/form-data 替换指定账号的头像。
    /// </summary>
    [HttpPut("{id}/avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(AiAccountMediaService.AvatarMaximumLength + 1024 * 1024)]
    [ProducesResponseType(typeof(AiAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<AiAccountResponse>> UploadAvatar(
        Guid id,
        [FromForm] UploadAiAccountMediaRequest request,
        CancellationToken cancellationToken)
    {
        return UploadMedia(
            id,
            LocalMediaKind.Avatar,
            request,
            cancellationToken);
    }

    /// <summary>
    /// 返回指定账号当前保存的头像图片。
    /// </summary>
    [HttpGet("{id}/avatar")]
    [Produces("image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetAvatar(Guid id)
    {
        return GetMedia(id, LocalMediaKind.Avatar);
    }

    /// <summary>
    /// 使用 multipart/form-data 替换指定账号的主页封面。
    /// </summary>
    [HttpPut("{id}/cover")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(AiAccountMediaService.CoverMaximumLength + 1024 * 1024)]
    [ProducesResponseType(typeof(AiAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<AiAccountResponse>> UploadCover(
        Guid id,
        [FromForm] UploadAiAccountMediaRequest request,
        CancellationToken cancellationToken)
    {
        return UploadMedia(
            id,
            LocalMediaKind.ProfileCover,
            request,
            cancellationToken);
    }

    /// <summary>
    /// 返回指定账号当前保存的主页封面图片。
    /// </summary>
    [HttpGet("{id}/cover")]
    [Produces("image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetCover(Guid id)
    {
        return GetMedia(id, LocalMediaKind.ProfileCover);
    }

    private async Task<ActionResult<AiAccountResponse>> UploadMedia(
        Guid aiAccountId,
        LocalMediaKind mediaKind,
        UploadAiAccountMediaRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new { message = "请选择一个非空图片文件。" });
        }

        await using Stream source = request.File.OpenReadStream();
        AiAccountMediaUploadResult result = await _aiAccountMediaService
            .UploadAsync(
                aiAccountId,
                mediaKind,
                source,
                request.File.Length,
                cancellationToken);

        if (result.Status == AiAccountMediaUploadStatus.AccountNotFound)
        {
            return NotFound();
        }

        if (result.Status == AiAccountMediaUploadStatus.TooLarge)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new { message = result.ErrorMessage });
        }

        if (result.Status is AiAccountMediaUploadStatus.Empty
            or AiAccountMediaUploadStatus.UnsupportedFormat)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        if (result.Status != AiAccountMediaUploadStatus.Succeeded
            || result.AiAccount is null)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = result.ErrorMessage });
        }

        return Ok(AiAccountResponseMapper.ToResponse(result.AiAccount));
    }

    private IActionResult GetMedia(
        Guid aiAccountId,
        LocalMediaKind mediaKind)
    {
        AiAccountMediaReadResult result = _aiAccountMediaService.OpenRead(
            aiAccountId,
            mediaKind);

        if (result.Content is null || result.MediaId is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        Response.Headers.ETag = $"\"{result.MediaId}\"";

        return File(
            result.Content.Stream,
            result.Content.ContentType,
            enableRangeProcessing: true);
    }

    /// <summary>
    /// 将可选的 HTTP 字符串转换为明确的档案枚举；留空时使用业务默认值。
    /// </summary>
    private static bool TryParseProfileEnum<TEnum>(
        string? value,
        TEnum defaultValue,
        out TEnum result)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = defaultValue;
            return true;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out result)
            && Enum.IsDefined(result);
    }
}
