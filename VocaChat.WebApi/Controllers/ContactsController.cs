using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.Contacts;
using VocaChat.WebApi.Dtos.PrivateChats;
using VocaChat.WebApi.Mapping;

namespace VocaChat.WebApi.Controllers;

/// <summary>提供好友查询、分组调整和进入私聊的 API。</summary>
[ApiController]
[Route("api/contacts")]
public sealed class ContactsController : ControllerBase
{
    private readonly ContactService _contactService;
    private readonly PrivateChatService _privateChatService;

    public ContactsController(
        ContactService contactService,
        PrivateChatService privateChatService)
    {
        _contactService = contactService;
        _privateChatService = privateChatService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ContactResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ContactResponse>> GetAll()
    {
        return Ok(_contactService.GetAllContacts()
            .Select(ContactResponseMapper.ToResponse)
            .ToList());
    }

    [HttpPut("{contactId}/group")]
    [ProducesResponseType(typeof(ContactResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ContactResponse> MoveToGroup(
        Guid contactId,
        [FromBody] MoveContactRequest request)
    {
        if (_contactService.FindById(contactId) is null)
        {
            return NotFound();
        }

        if (!_contactService.TryMoveContact(
                contactId,
                request.ContactGroupId,
                out Contact? contact,
                out string errorMessage)
            || contact is null)
        {
            return BadRequest(new { message = errorMessage });
        }

        return Ok(ContactResponseMapper.ToResponse(contact));
    }

    [HttpPut("{contactId}/private-chat")]
    [ProducesResponseType(typeof(PrivateChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PrivateChatResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<PrivateChatResponse> GetOrCreatePrivateChat(
        Guid contactId)
    {
        if (!_privateChatService.TryGetOrCreate(
                contactId,
                out PrivateChat? privateChat,
                out bool created,
                out string errorMessage)
            || privateChat is null)
        {
            return BadRequest(new { message = errorMessage });
        }

        PrivateChatResponse response =
            PrivateChatResponseMapper.ToResponse(privateChat);

        return created
            ? CreatedAtAction(
                nameof(PrivateChatsController.GetById),
                "PrivateChats",
                new { id = privateChat.Id },
                response)
            : Ok(response);
    }
}
