using VocaChat.Models;
using VocaChat.WebApi.Dtos.Contacts;

namespace VocaChat.WebApi.Mapping;

/// <summary>将好友关系实体映射为 HTTP 响应。</summary>
public static class ContactResponseMapper
{
    public static ContactResponse ToResponse(Contact contact)
    {
        return new ContactResponse
        {
            Id = contact.Id,
            ContactGroupId = contact.ContactGroupId,
            ContactGroupName = contact.ContactGroup.Name,
            Friend = AiAccountResponseMapper.ToResponse(contact.AiAccount),
            CreatedAt = contact.CreatedAt
        };
    }
}
