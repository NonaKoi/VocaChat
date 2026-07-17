using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.Contacts;

namespace VocaChat.WebApi.Controllers;

/// <summary>提供好友分组查询和创建 API。</summary>
[ApiController]
[Route("api/contact-groups")]
public sealed class ContactGroupsController : ControllerBase
{
    private readonly ContactService _contactService;

    public ContactGroupsController(ContactService contactService)
    {
        _contactService = contactService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ContactGroupResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ContactGroupResponse>> GetAll()
    {
        return Ok(_contactService.GetAllGroups()
            .Select(ToResponse)
            .ToList());
    }

    [HttpPost]
    [ProducesResponseType(typeof(ContactGroupResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ContactGroupResponse> Create(
        [FromBody] CreateContactGroupRequest request)
    {
        if (!_contactService.TryCreateGroup(
                request.Name,
                out ContactGroup? group,
                out string errorMessage)
            || group is null)
        {
            return BadRequest(new { message = errorMessage });
        }

        return StatusCode(
            StatusCodes.Status201Created,
            ToResponse(group));
    }

    private static ContactGroupResponse ToResponse(ContactGroup group)
    {
        return new ContactGroupResponse
        {
            Id = group.Id,
            Name = group.Name,
            SortOrder = group.SortOrder,
            CreatedAt = group.CreatedAt
        };
    }
}
