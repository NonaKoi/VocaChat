using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 协调一次受控的好友自主私信：判断、建会话、生成消息、保存并记录互动。
/// </summary>
public sealed class AutonomousPrivateChatExecutionService
{
    private readonly AutonomousPrivateChatJudge _privateChatJudge;
    private readonly AiAccountService _aiAccountService;
    private readonly PrivateChatService _privateChatService;
    private readonly FakeAiReplyService _fakeAiReplyService;
    private readonly AiRelationshipService _aiRelationshipService;

    public AutonomousPrivateChatExecutionService(
        AutonomousPrivateChatJudge privateChatJudge,
        AiAccountService aiAccountService,
        PrivateChatService privateChatService,
        FakeAiReplyService fakeAiReplyService,
        AiRelationshipService aiRelationshipService)
    {
        _privateChatJudge = privateChatJudge;
        _aiAccountService = aiAccountService;
        _privateChatService = privateChatService;
        _fakeAiReplyService = fakeAiReplyService;
        _aiRelationshipService = aiRelationshipService;
    }

    /// <summary>
    /// 只对指定好友组合执行一次判断；判断拒绝时不会创建任何会话或消息。
    /// </summary>
    public AutonomousPrivateChatExecutionResult Execute(
        Guid firstAiAccountId,
        Guid secondAiAccountId,
        DateTime evaluatedAt,
        double randomJitter)
    {
        AutonomousPrivateChatDecision decision = _privateChatJudge.Evaluate(
            firstAiAccountId,
            secondAiAccountId,
            evaluatedAt,
            randomJitter);

        if (!decision.IsApproved)
        {
            return CreateResult(
                AutonomousPrivateChatExecutionStatus.DecisionRejected,
                decision);
        }

        AiAccount? initiator = decision.InitiatorAiAccountId is Guid initiatorId
            ? _aiAccountService.FindById(initiatorId)
            : null;
        AiAccount? recipient = decision.RecipientAiAccountId is Guid recipientId
            ? _aiAccountService.FindById(recipientId)
            : null;

        if (initiator is null || recipient is null)
        {
            return CreateResult(
                AutonomousPrivateChatExecutionStatus.ChatCreationFailed,
                decision,
                errorMessage: "判断通过后未能读取完整的好友资料。");
        }

        if (!_privateChatService.TryGetOrCreateAiPrivateChat(
                initiator.Id,
                recipient.Id,
                out PrivateChat? privateChat,
                out bool privateChatCreated,
                out string chatError)
            || privateChat is null)
        {
            return CreateResult(
                AutonomousPrivateChatExecutionStatus.ChatCreationFailed,
                decision,
                errorMessage: chatError);
        }

        string openingContent =
            _fakeAiReplyService.GenerateAutonomousPrivateChatOpening(
                initiator,
                recipient);
        string replyContent =
            _fakeAiReplyService.GenerateAutonomousPrivateChatReply(
                recipient,
                initiator,
                openingContent);

        if (!_privateChatService.TrySaveAiExchange(
                privateChat,
                initiator,
                openingContent,
                recipient,
                replyContent,
                evaluatedAt,
                out PrivateMessage? initiatorMessage,
                out PrivateMessage? recipientReply,
                out string messageError))
        {
            return CreateResult(
                AutonomousPrivateChatExecutionStatus.MessagePersistenceFailed,
                decision,
                privateChat,
                privateChatCreated,
                errorMessage: messageError);
        }

        AiRelationshipOperationStatus relationshipStatus =
            _aiRelationshipService.TryRecordInteraction(
                initiator.Id,
                recipient.Id,
                evaluatedAt);

        if (relationshipStatus != AiRelationshipOperationStatus.Success)
        {
            return CreateResult(
                AutonomousPrivateChatExecutionStatus.RelationshipRecordFailed,
                decision,
                privateChat,
                privateChatCreated,
                initiatorMessage,
                recipientReply,
                "消息已经保存，但关系互动时间更新失败。");
        }

        return CreateResult(
            AutonomousPrivateChatExecutionStatus.Completed,
            decision,
            privateChat,
            privateChatCreated,
            initiatorMessage,
            recipientReply);
    }

    private static AutonomousPrivateChatExecutionResult CreateResult(
        AutonomousPrivateChatExecutionStatus status,
        AutonomousPrivateChatDecision decision,
        PrivateChat? privateChat = null,
        bool privateChatCreated = false,
        PrivateMessage? initiatorMessage = null,
        PrivateMessage? recipientReply = null,
        string errorMessage = "")
    {
        return new AutonomousPrivateChatExecutionResult
        {
            Status = status,
            Decision = decision,
            PrivateChat = privateChat,
            PrivateChatCreated = privateChatCreated,
            InitiatorMessage = initiatorMessage,
            RecipientReply = recipientReply,
            ErrorMessage = errorMessage
        };
    }
}
