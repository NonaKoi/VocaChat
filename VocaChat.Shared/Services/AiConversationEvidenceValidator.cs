using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 集中验证一个 AI 账号是否真实看见过指定私聊或群聊消息。
/// </summary>
internal static class AiConversationEvidenceValidator
{
    public static bool TryValidate(
        VocaChatDbContext dbContext,
        Guid observerAiAccountId,
        Guid? sourcePrivateMessageId,
        Guid? sourceGroupMessageId,
        out AiConversationEvidenceValidationStatus status,
        out ValidatedConversationEvidence? evidence,
        out string errorMessage)
    {
        status = AiConversationEvidenceValidationStatus.InvalidSource;
        evidence = null;

        if (sourcePrivateMessageId.HasValue
            == sourceGroupMessageId.HasValue)
        {
            errorMessage = "世界认知来源必须且只能选择一条私聊或群聊消息。";
            return false;
        }

        if (sourcePrivateMessageId.HasValue)
        {
            return TryValidatePrivateMessage(
                dbContext,
                observerAiAccountId,
                sourcePrivateMessageId.Value,
                out status,
                out evidence,
                out errorMessage);
        }

        return TryValidateGroupMessage(
            dbContext,
            observerAiAccountId,
            sourceGroupMessageId!.Value,
            out status,
            out evidence,
            out errorMessage);
    }

    private static bool TryValidatePrivateMessage(
        VocaChatDbContext dbContext,
        Guid observerAiAccountId,
        Guid sourcePrivateMessageId,
        out AiConversationEvidenceValidationStatus status,
        out ValidatedConversationEvidence? evidence,
        out string errorMessage)
    {
        status = AiConversationEvidenceValidationStatus.InvalidSource;
        evidence = null;
        PrivateMessage? message = dbContext.PrivateMessages
            .AsNoTracking()
            .SingleOrDefault(item => item.Id == sourcePrivateMessageId);

        if (message is null)
        {
            status = AiConversationEvidenceValidationStatus.SourceNotFound;
            errorMessage = "来源私聊消息不存在。";
            return false;
        }

        PrivateChat? privateChat = dbContext.PrivateChats
            .AsNoTracking()
            .SingleOrDefault(chat => chat.Id == message.PrivateChatId);

        if (privateChat is null
            || !IsPrivateChatParticipant(
                dbContext,
                privateChat,
                observerAiAccountId))
        {
            status = AiConversationEvidenceValidationStatus.SourceNotVisible;
            errorMessage = "该 AI 账号没有参与来源私聊，不能获得这条知识。";
            return false;
        }

        if (message.SenderAiAccountId == observerAiAccountId)
        {
            status = AiConversationEvidenceValidationStatus.SelfAuthoredSource;
            errorMessage = "AI 账号不能把自己发送的消息作为新知识来源。";
            return false;
        }

        evidence = new ValidatedConversationEvidence(
            message.SenderType,
            message.SenderAiAccountId,
            message.Id,
            SourceGroupMessageId: null,
            message.SentAt);
        status = AiConversationEvidenceValidationStatus.Success;
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateGroupMessage(
        VocaChatDbContext dbContext,
        Guid observerAiAccountId,
        Guid sourceGroupMessageId,
        out AiConversationEvidenceValidationStatus status,
        out ValidatedConversationEvidence? evidence,
        out string errorMessage)
    {
        status = AiConversationEvidenceValidationStatus.InvalidSource;
        evidence = null;
        GroupMessage? message = dbContext.GroupMessages
            .AsNoTracking()
            .SingleOrDefault(item => item.Id == sourceGroupMessageId);

        if (message is null)
        {
            status = AiConversationEvidenceValidationStatus.SourceNotFound;
            errorMessage = "来源群消息不存在。";
            return false;
        }

        bool wasVisible = dbContext.GroupMessageAudience
            .AsNoTracking()
            .Any(audience =>
                audience.GroupMessageId == sourceGroupMessageId
                && audience.AiAccountId == observerAiAccountId);

        if (!wasVisible)
        {
            status = AiConversationEvidenceValidationStatus.SourceNotVisible;
            errorMessage = "该 AI 账号不在来源群消息的接收者快照中。";
            return false;
        }

        if (message.SenderAiAccountId == observerAiAccountId)
        {
            status = AiConversationEvidenceValidationStatus.SelfAuthoredSource;
            errorMessage = "AI 账号不能把自己发送的消息作为新知识来源。";
            return false;
        }

        evidence = new ValidatedConversationEvidence(
            message.SenderType,
            message.SenderAiAccountId,
            SourcePrivateMessageId: null,
            message.Id,
            message.SentAt);
        status = AiConversationEvidenceValidationStatus.Success;
        errorMessage = string.Empty;
        return true;
    }

    private static bool IsPrivateChatParticipant(
        VocaChatDbContext dbContext,
        PrivateChat privateChat,
        Guid aiAccountId)
    {
        if (privateChat.Kind == PrivateChatKind.AiAccounts)
        {
            return privateChat.FirstAiAccountId == aiAccountId
                || privateChat.SecondAiAccountId == aiAccountId;
        }

        return privateChat.ContactId.HasValue
            && dbContext.Contacts
                .AsNoTracking()
                .Any(contact =>
                    contact.Id == privateChat.ContactId.Value
                    && contact.AiAccountId == aiAccountId);
    }
}

internal enum AiConversationEvidenceValidationStatus
{
    Success,
    InvalidSource,
    SourceNotFound,
    SourceNotVisible,
    SelfAuthoredSource
}

/// <summary>
/// 表示已经确认对指定 AI 可见的正式消息来源。
/// </summary>
internal sealed record ValidatedConversationEvidence(
    MessageSenderType SourceType,
    Guid? SourceAiAccountId,
    Guid? SourcePrivateMessageId,
    Guid? SourceGroupMessageId,
    DateTime ObservedAt);
