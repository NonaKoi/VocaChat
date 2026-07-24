using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.CharacterWorlds;
using VocaChat.WebApi.Mapping;

namespace VocaChat.WebApi.Controllers;

/// <summary>
/// 提供角色世界查询、创建和更新 API。
/// </summary>
[ApiController]
[Route("api/character-worlds")]
public sealed class CharacterWorldsController : ControllerBase
{
    private readonly CharacterWorldService _characterWorldService;

    public CharacterWorldsController(
        CharacterWorldService characterWorldService)
    {
        _characterWorldService = characterWorldService;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<CharacterWorldResponse>),
        StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<CharacterWorldResponse>> GetAll()
    {
        List<CharacterWorldResponse> response = _characterWorldService
            .GetAll()
            .Select(CharacterWorldResponseMapper.ToResponse)
            .ToList();

        return Ok(response);
    }

    [HttpGet("{id}", Name = "GetCharacterWorldById")]
    [ProducesResponseType(
        typeof(CharacterWorldResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<CharacterWorldResponse> GetById(Guid id)
    {
        CharacterWorld? characterWorld =
            _characterWorldService.FindById(id);

        return characterWorld is null
            ? NotFound()
            : Ok(CharacterWorldResponseMapper.ToResponse(characterWorld));
    }

    [HttpPost]
    [ProducesResponseType(
        typeof(CharacterWorldResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<CharacterWorldResponse> Create(
        [FromBody] CreateCharacterWorldRequest request)
    {
        CharacterWorldOperationStatus status =
            _characterWorldService.TryCreate(
                request.Name ?? string.Empty,
                request.Description ?? string.Empty,
                out CharacterWorld? characterWorld,
                out string errorMessage);

        if (status == CharacterWorldOperationStatus.PersistenceFailed)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = errorMessage });
        }

        if (status != CharacterWorldOperationStatus.Success
            || characterWorld is null)
        {
            return BadRequest(new { message = errorMessage });
        }

        CharacterWorldResponse response =
            CharacterWorldResponseMapper.ToResponse(characterWorld);

        return CreatedAtAction(
            nameof(GetById),
            new { id = characterWorld.Id },
            response);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(
        typeof(CharacterWorldResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<CharacterWorldResponse> Update(
        Guid id,
        [FromBody] UpdateCharacterWorldRequest request)
    {
        CharacterWorldOperationStatus status =
            _characterWorldService.TryUpdate(
                id,
                request.Name ?? string.Empty,
                request.Description ?? string.Empty,
                out CharacterWorld? characterWorld,
                out string errorMessage);

        if (status == CharacterWorldOperationStatus.NotFound)
        {
            return NotFound();
        }

        if (status == CharacterWorldOperationStatus.PersistenceFailed)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = errorMessage });
        }

        if (status != CharacterWorldOperationStatus.Success
            || characterWorld is null)
        {
            return BadRequest(new { message = errorMessage });
        }

        return Ok(CharacterWorldResponseMapper.ToResponse(characterWorld));
    }
}
