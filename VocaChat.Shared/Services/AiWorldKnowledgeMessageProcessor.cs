using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 在正式消息保存后识别合法监听者，并将一条消息的一次提取结果
/// 分发为各监听者自己的世界知识和方向性认知。
/// </summary>
public sealed class AiWorldKnowledgeMessageProcessor
{
    private static readonly string[] UserSemanticWorldCues =
    {
        "世界", "现实", "时空", "宇宙", "维度", "来自", "故乡",
        "家乡", "你们", "彼此", "通信", "联系", "生活", "常识",
        "环境", "地方"
    };

    private readonly VocaChatDbContextFactory _dbContextFactory;
    private readonly AiWorldKnowledgeCandidateExtractor _candidateExtractor;
    private readonly AiWorldKnowledgeService _knowledgeService;
    private readonly AiWorldAwarenessService _awarenessService;
    private readonly AiInteractionDiagnosticLogService?
        _diagnosticLogService;

    public AiWorldKnowledgeMessageProcessor(
        VocaChatDbContextFactory dbContextFactory,
        AiWorldKnowledgeCandidateExtractor candidateExtractor,
        AiWorldKnowledgeService knowledgeService,
        AiWorldAwarenessService awarenessService,
        AiInteractionDiagnosticLogService? diagnosticLogService = null)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _candidateExtractor = candidateExtractor
            ?? throw new ArgumentNullException(nameof(candidateExtractor));
        _knowledgeService = knowledgeService
            ?? throw new ArgumentNullException(nameof(knowledgeService));
        _awarenessService = awarenessService
            ?? throw new ArgumentNullException(nameof(awarenessService));
        _diagnosticLogService = diagnosticLogService;
    }

    /// <summary>
    /// 处理一条已保存私聊消息。AI 与 AI 私聊只允许另一位参与者学习，
    /// 本地用户私聊只允许当前 AI 好友接收用户明确提供的信息。
    /// </summary>
    public Task<AiWorldKnowledgeMessageProcessingResult>
        ProcessPrivateMessageAsync(
        Guid privateMessageId,
        CancellationToken cancellationToken = default)
    {
        return ProcessPrivateMessageAsync(
            privateMessageId,
            usageCorrelationOverride: null,
            cancellationToken);
    }

    /// <summary>
    /// 允许聊天协调流程提供即将生成回复的批次关联，使用户消息触发的
    /// 语义提取用量能够归入同一轮可见回复。
    /// </summary>
    public async Task<AiWorldKnowledgeMessageProcessingResult>
        ProcessPrivateMessageAsync(
        Guid privateMessageId,
        AiModelUsageCorrelation? usageCorrelationOverride,
        CancellationToken cancellationToken = default)
    {
        Guid? sourceAiAccountId = null;
        Guid? privateChatId = null;
        AiMessageGenerationScenario scenario =
            AiMessageGenerationScenario.UserPrivateChat;

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            PrivateMessage? message = dbContext.PrivateMessages
                .AsNoTracking()
                .SingleOrDefault(item => item.Id == privateMessageId);
            if (message is null)
            {
                return CreateResult(
                    AiWorldKnowledgeMessageProcessingStatus.MessageNotFound,
                    errors: new[] { "私聊消息不存在，不能处理世界知识。" });
            }

            sourceAiAccountId = message.SenderAiAccountId;
            privateChatId = message.PrivateChatId;
            scenario = message.AutonomousPrivateChatSessionId.HasValue
                ? AiMessageGenerationScenario.AutonomousPrivateChat
                : AiMessageGenerationScenario.UserPrivateChat;
            PrivateChat? privateChat = dbContext.PrivateChats
                .AsNoTracking()
                .SingleOrDefault(item => item.Id == message.PrivateChatId);
            if (privateChat is null)
            {
                return CreateResult(
                    AiWorldKnowledgeMessageProcessingStatus.MessageNotFound,
                    errors: new[] { "私聊不存在，不能处理世界知识。" });
            }

            AiAccount? sourceAiAccount = FindSourceAccount(
                dbContext,
                message.SenderAiAccountId);
            IReadOnlyList<AiAccount> listeners = ReadPrivateListeners(
                dbContext,
                privateChat,
                message.SenderAiAccountId);
            AiModelUsageCorrelation usageCorrelation =
                usageCorrelationOverride
                ?? new AiModelUsageCorrelation
                {
                    PrivateChatId = message.PrivateChatId,
                    AutonomousPrivateChatSessionId =
                        message.AutonomousPrivateChatSessionId,
                    AiResponseBatchId = message.AiResponseBatchId
                };
            AiWorldKnowledgeExtraction extraction =
                await _candidateExtractor.ExtractAsync(
                    sourceAiAccount,
                    message.Content,
                    ShouldUseSemanticFallback(
                        sourceAiAccount,
                        listeners,
                        message.Content),
                    usageCorrelation,
                    cancellationToken);

            AiWorldKnowledgeMessageProcessingResult result =
                ProcessForListeners(
                extraction,
                sourceAiAccount,
                listeners,
                message.Content,
                sourcePrivateMessageId: message.Id,
                sourceGroupMessageId: null);
            RecordFailureIfNeeded(
                result,
                scenario,
                sourceAiAccountId,
                privateChatId);
            return result;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AiWorldKnowledgeMessageProcessingResult result = CreateResult(
                AiWorldKnowledgeMessageProcessingStatus.PersistenceFailed,
                errors: new[]
                {
                    exception is SqliteException
                        ? "私聊世界知识后处理暂时无法读取数据库。"
                        : "私聊消息已经保存，但世界知识后处理发生异常。"
                });
            RecordFailureIfNeeded(
                result,
                scenario,
                sourceAiAccountId,
                privateChatId);
            return result;
        }
    }

    /// <summary>
    /// 处理一条已保存群消息，并严格使用消息落库时的 AI 接收者快照。
    /// </summary>
    public async Task<AiWorldKnowledgeMessageProcessingResult>
        ProcessGroupMessageAsync(
        Guid groupMessageId,
        CancellationToken cancellationToken = default)
    {
        Guid? sourceAiAccountId = null;
        Guid? groupChatId = null;
        AiMessageGenerationScenario scenario =
            AiMessageGenerationScenario.GroupPrimaryReply;

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            GroupMessage? message = dbContext.GroupMessages
                .AsNoTracking()
                .SingleOrDefault(item => item.Id == groupMessageId);
            if (message is null)
            {
                return CreateResult(
                    AiWorldKnowledgeMessageProcessingStatus.MessageNotFound,
                    errors: new[] { "群消息不存在，不能处理世界知识。" });
            }

            sourceAiAccountId = message.SenderAiAccountId;
            groupChatId = message.GroupChatId;
            scenario = message.AutonomousGroupChatSessionId.HasValue
                ? AiMessageGenerationScenario.AutonomousGroupChat
                : AiMessageGenerationScenario.GroupPrimaryReply;
            List<Guid> listenerIds = dbContext.GroupMessageAudience
                .AsNoTracking()
                .Where(item => item.GroupMessageId == message.Id)
                .Select(item => item.AiAccountId)
                .Distinct()
                .ToList();
            IReadOnlyList<AiAccount> listeners = dbContext.AiAccounts
                .AsNoTracking()
                .Where(account => listenerIds.Contains(account.Id))
                .ToList()
                .AsReadOnly();
            AiAccount? sourceAiAccount = FindSourceAccount(
                dbContext,
                message.SenderAiAccountId);
            AiWorldKnowledgeExtraction extraction =
                await _candidateExtractor.ExtractAsync(
                    sourceAiAccount,
                    message.Content,
                    ShouldUseSemanticFallback(
                        sourceAiAccount,
                        listeners,
                        message.Content),
                    new AiModelUsageCorrelation
                    {
                        GroupChatId = message.GroupChatId,
                        AutonomousGroupChatSessionId =
                            message.AutonomousGroupChatSessionId,
                        InteractionBatchId = message.InteractionBatchId,
                        AiResponseBatchId = message.AiResponseBatchId
                    },
                    cancellationToken);

            AiWorldKnowledgeMessageProcessingResult result =
                ProcessForListeners(
                extraction,
                sourceAiAccount,
                listeners,
                message.Content,
                sourcePrivateMessageId: null,
                sourceGroupMessageId: message.Id);
            RecordFailureIfNeeded(
                result,
                scenario,
                sourceAiAccountId,
                groupChatId);
            return result;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AiWorldKnowledgeMessageProcessingResult result = CreateResult(
                AiWorldKnowledgeMessageProcessingStatus.PersistenceFailed,
                errors: new[]
                {
                    exception is SqliteException
                        ? "群聊世界知识后处理暂时无法读取数据库。"
                        : "群消息已经保存，但世界知识后处理发生异常。"
                });
            RecordFailureIfNeeded(
                result,
                scenario,
                sourceAiAccountId,
                groupChatId);
            return result;
        }
    }

    private AiWorldKnowledgeMessageProcessingResult ProcessForListeners(
        AiWorldKnowledgeExtraction extraction,
        AiAccount? sourceAiAccount,
        IReadOnlyList<AiAccount> listeners,
        string sourceContent,
        Guid? sourcePrivateMessageId,
        Guid? sourceGroupMessageId)
    {
        if (!string.IsNullOrWhiteSpace(extraction.ErrorMessage))
        {
            return CreateResult(
                AiWorldKnowledgeMessageProcessingStatus.PartialFailure,
                errors: new[] { extraction.ErrorMessage });
        }

        if (listeners.Count == 0
            || (extraction.Signal == AiWorldKnowledgeSignal.None
                && extraction.Candidates.Count == 0))
        {
            return CreateResult(
                AiWorldKnowledgeMessageProcessingStatus.NoRelevantKnowledge);
        }

        List<Guid> knowledgeIds = new();
        HashSet<Guid> updatedObservers = new();
        HashSet<Guid> newlyInformed = new();
        List<string> errors = new();
        bool addedNewEvidence = false;

        if (sourceAiAccount is null)
        {
            ProcessUserSource(
                extraction.Signal,
                listeners,
                sourceContent,
                sourcePrivateMessageId,
                sourceGroupMessageId,
                knowledgeIds,
                updatedObservers,
                newlyInformed,
                errors,
                ref addedNewEvidence);
        }
        else
        {
            foreach (AiAccount listener in listeners)
            {
                ProcessAiSourceForListener(
                    extraction,
                    sourceAiAccount,
                    listener,
                    sourcePrivateMessageId,
                    sourceGroupMessageId,
                    knowledgeIds,
                    updatedObservers,
                    newlyInformed,
                    errors,
                    ref addedNewEvidence);
            }
        }

        if (errors.Count > 0)
        {
            return CreateResult(
                AiWorldKnowledgeMessageProcessingStatus.PartialFailure,
                knowledgeIds,
                updatedObservers,
                newlyInformed,
                errors);
        }

        bool changedAnything = addedNewEvidence
            || updatedObservers.Count > 0
            || newlyInformed.Count > 0;
        return CreateResult(
            changedAnything
                ? AiWorldKnowledgeMessageProcessingStatus.Success
                : AiWorldKnowledgeMessageProcessingStatus.AlreadyProcessed,
            knowledgeIds,
            updatedObservers,
            newlyInformed);
    }

    private static bool ShouldUseSemanticFallback(
        AiAccount? sourceAiAccount,
        IReadOnlyList<AiAccount> listeners,
        string content)
    {
        string normalizedContent = content?.Trim() ?? string.Empty;
        if (listeners.Count == 0 || normalizedContent.Length < 6)
        {
            return false;
        }

        if (sourceAiAccount is not null)
        {
            return listeners.Any(listener =>
                listener.Id != sourceAiAccount.Id
                && listener.CharacterWorldId
                    != sourceAiAccount.CharacterWorldId);
        }

        return UserSemanticWorldCues.Any(cue => normalizedContent.Contains(
            cue,
            StringComparison.OrdinalIgnoreCase));
    }

    private void ProcessAiSourceForListener(
        AiWorldKnowledgeExtraction extraction,
        AiAccount sourceAiAccount,
        AiAccount listener,
        Guid? sourcePrivateMessageId,
        Guid? sourceGroupMessageId,
        List<Guid> knowledgeIds,
        HashSet<Guid> updatedObservers,
        HashSet<Guid> newlyInformed,
        List<string> errors,
        ref bool addedNewEvidence)
    {
        if (listener.Id == sourceAiAccount.Id)
        {
            return;
        }

        if (extraction.Signal is
            AiWorldKnowledgeSignal.ParallelWorldInformation
            or AiWorldKnowledgeSignal.ExplicitCrossWorldConfirmation)
        {
            TryInformAboutParallelWorlds(
                listener.Id,
                sourcePrivateMessageId,
                sourceGroupMessageId,
                newlyInformed,
                errors);
        }

        if (listener.CharacterWorldId == sourceAiAccount.CharacterWorldId)
        {
            return;
        }

        foreach (AiWorldKnowledgeCandidate candidate
                 in extraction.Candidates)
        {
            AiWorldKnowledgeOperationStatus knowledgeStatus =
                _knowledgeService.TryCreateKnowledge(
                    new AiWorldKnowledgeWriteData(
                        listener.Id,
                        candidate.SubjectCharacterWorldId,
                        candidate.SubjectAiAccountId,
                        candidate.KnowledgeKey,
                        candidate.Summary,
                        candidate.FactNature,
                        candidate.Mutability,
                        candidate.TrustLevel,
                        candidate.Salience,
                        IsUserLocked: false),
                    sourcePrivateMessageId,
                    sourceGroupMessageId,
                    candidate.Summary,
                    out AiWorldKnowledge? knowledge,
                    out string knowledgeError);

            if (knowledgeStatus is not (
                    AiWorldKnowledgeOperationStatus.Success
                    or AiWorldKnowledgeOperationStatus.EvidenceAdded
                    or AiWorldKnowledgeOperationStatus.KnowledgeSuperseded
                    or AiWorldKnowledgeOperationStatus
                        .ConflictCandidateCreated
                    or AiWorldKnowledgeOperationStatus.AlreadyExists))
            {
                errors.Add(knowledgeError);
                continue;
            }

            if (knowledge is not null
                && !knowledgeIds.Contains(knowledge.Id))
            {
                knowledgeIds.Add(knowledge.Id);
            }

            if (knowledgeStatus
                == AiWorldKnowledgeOperationStatus.AlreadyExists)
            {
                continue;
            }

            addedNewEvidence = true;
            TryAdvanceDirectionalAwareness(
                listener.Id,
                sourceAiAccount.Id,
                candidate.Signal,
                sourcePrivateMessageId,
                sourceGroupMessageId,
                updatedObservers,
                errors);
        }
    }

    private void ProcessUserSource(
        AiWorldKnowledgeSignal signal,
        IReadOnlyList<AiAccount> listeners,
        string sourceContent,
        Guid? sourcePrivateMessageId,
        Guid? sourceGroupMessageId,
        List<Guid> knowledgeIds,
        HashSet<Guid> updatedObservers,
        HashSet<Guid> newlyInformed,
        List<string> errors,
        ref bool addedNewEvidence)
    {
        if (signal is not (
                AiWorldKnowledgeSignal.ParallelWorldInformation
                or AiWorldKnowledgeSignal.ExplicitCrossWorldConfirmation))
        {
            return;
        }

        foreach (AiAccount listener in listeners)
        {
            TryInformAboutParallelWorlds(
                listener.Id,
                sourcePrivateMessageId,
                sourceGroupMessageId,
                newlyInformed,
                errors);
        }

        if (signal
                != AiWorldKnowledgeSignal.ExplicitCrossWorldConfirmation
            || sourceGroupMessageId is null)
        {
            return;
        }

        foreach (AiAccount observer in listeners)
        {
            foreach (AiAccount subject in listeners.Where(subject =>
                         subject.Id != observer.Id
                         && subject.CharacterWorldId
                            != observer.CharacterWorldId))
            {
                string knowledgeKey = CreateUserConfirmationKnowledgeKey(
                    subject,
                    sourceContent);
                string summary = TruncateKnowledgeSummary(
                    $"本地用户明确说明 {subject.Nickname} 与当前角色来自不同世界：{sourceContent.Trim()}");
                AiWorldKnowledgeOperationStatus status =
                    _knowledgeService.TryCreateKnowledge(
                        new AiWorldKnowledgeWriteData(
                            observer.Id,
                            subject.CharacterWorldId,
                            subject.Id,
                            knowledgeKey,
                            summary,
                            AiWorldKnowledgeFactNature.Unconfirmed,
                            AiWorldKnowledgeMutability.Constant,
                            AiWorldKnowledgeTrustLevel.UserConfirmed,
                            Salience: 95,
                            IsUserLocked: false),
                        sourcePrivateMessageId: null,
                        sourceGroupMessageId,
                        summary,
                        out AiWorldKnowledge? knowledge,
                        out string knowledgeError);

                if (status is not (
                        AiWorldKnowledgeOperationStatus.Success
                        or AiWorldKnowledgeOperationStatus.EvidenceAdded
                        or AiWorldKnowledgeOperationStatus.KnowledgeSuperseded
                        or AiWorldKnowledgeOperationStatus
                            .ConflictCandidateCreated
                        or AiWorldKnowledgeOperationStatus.AlreadyExists))
                {
                    errors.Add(knowledgeError);
                    continue;
                }

                if (knowledge is not null
                    && !knowledgeIds.Contains(knowledge.Id))
                {
                    knowledgeIds.Add(knowledge.Id);
                }

                if (status
                    == AiWorldKnowledgeOperationStatus.AlreadyExists)
                {
                    continue;
                }

                addedNewEvidence = true;
                TryAdvanceDirectionalAwareness(
                    observer.Id,
                    subject.Id,
                    AiWorldKnowledgeSignal.ExplicitCrossWorldConfirmation,
                    sourcePrivateMessageId: null,
                    sourceGroupMessageId,
                    updatedObservers,
                    errors);
            }
        }
    }

    private void TryInformAboutParallelWorlds(
        Guid listenerAiAccountId,
        Guid? sourcePrivateMessageId,
        Guid? sourceGroupMessageId,
        HashSet<Guid> newlyInformed,
        List<string> errors)
    {
        AiWorldAwarenessOperationStatus readStatus =
            _awarenessService.TryGetParallelWorldAwareness(
                listenerAiAccountId,
                out AiParallelWorldAwarenessState currentState,
                out _,
                out string readError);
        if (readStatus != AiWorldAwarenessOperationStatus.Success)
        {
            errors.Add(readError);
            return;
        }

        if (currentState != AiParallelWorldAwarenessState.Unaware)
        {
            return;
        }

        AiWorldAwarenessOperationStatus updateStatus =
            _awarenessService.TryRecordParallelWorldAwareness(
                listenerAiAccountId,
                AiParallelWorldAwarenessState.Informed,
                sourcePrivateMessageId,
                sourceGroupMessageId,
                out _,
                out string updateError);

        if (updateStatus == AiWorldAwarenessOperationStatus.Success)
        {
            newlyInformed.Add(listenerAiAccountId);
        }
        else if (updateStatus != AiWorldAwarenessOperationStatus.UserLocked)
        {
            errors.Add(updateError);
        }
    }

    private void TryAdvanceDirectionalAwareness(
        Guid observerAiAccountId,
        Guid subjectAiAccountId,
        AiWorldKnowledgeSignal signal,
        Guid? sourcePrivateMessageId,
        Guid? sourceGroupMessageId,
        HashSet<Guid> updatedObservers,
        List<string> errors)
    {
        AwarenessEvidenceCounts counts = ReadAwarenessEvidenceCounts(
            observerAiAccountId,
            subjectAiAccountId);
        AiWorldAwareness? existing = ReadAwareness(
            observerAiAccountId,
            subjectAiAccountId);
        AiWorldAwarenessState currentState = existing?.State
            ?? AiWorldAwarenessState.AssumedSharedWorld;
        AiWorldAwarenessState targetState = SelectTargetState(
            currentState,
            signal,
            counts.EvidenceCount);

        if (targetState == AiWorldAwarenessState.AssumedSharedWorld)
        {
            return;
        }

        AiWorldAwarenessOperationStatus status =
            _awarenessService.TryRecordWorldAwareness(
                observerAiAccountId,
                subjectAiAccountId,
                targetState,
                counts.EvidenceCount,
                counts.DistinctConversationCount,
                sourcePrivateMessageId,
                sourceGroupMessageId,
                out _,
                out string errorMessage);

        if (status == AiWorldAwarenessOperationStatus.Success)
        {
            updatedObservers.Add(observerAiAccountId);
        }
        else if (status != AiWorldAwarenessOperationStatus.UserLocked)
        {
            errors.Add(errorMessage);
        }
    }

    private AwarenessEvidenceCounts ReadAwarenessEvidenceCounts(
        Guid ownerAiAccountId,
        Guid subjectAiAccountId)
    {
        using VocaChatDbContext dbContext =
            _dbContextFactory.CreateDbContext();
        List<AwarenessEvidenceSource> sources = (
                from evidence in dbContext.AiWorldKnowledgeEvidence
                    .AsNoTracking()
                join knowledge in dbContext.AiWorldKnowledge.AsNoTracking()
                    on evidence.AiWorldKnowledgeId equals knowledge.Id
                where knowledge.OwnerAiAccountId == ownerAiAccountId
                    && knowledge.SubjectAiAccountId == subjectAiAccountId
                    && knowledge.Status == AiWorldKnowledgeStatus.Active
                select new AwarenessEvidenceSource(
                    evidence.SourcePrivateMessageId,
                    evidence.SourceGroupMessageId))
            .ToList();

        HashSet<string> conversationKeys =
            new(StringComparer.Ordinal);
        List<Guid> privateMessageIds = sources
            .Where(item => item.PrivateMessageId.HasValue)
            .Select(item => item.PrivateMessageId!.Value)
            .Distinct()
            .ToList();
        foreach (PrivateMessage message in dbContext.PrivateMessages
                     .AsNoTracking()
                     .Where(message => privateMessageIds.Contains(message.Id)))
        {
            conversationKeys.Add(message.AutonomousPrivateChatSessionId
                    is Guid sessionId
                ? $"private-session:{sessionId:N}"
                : $"private-chat:{message.PrivateChatId:N}");
        }

        List<Guid> groupMessageIds = sources
            .Where(item => item.GroupMessageId.HasValue)
            .Select(item => item.GroupMessageId!.Value)
            .Distinct()
            .ToList();
        foreach (GroupMessage message in dbContext.GroupMessages
                     .AsNoTracking()
                     .Where(message => groupMessageIds.Contains(message.Id)))
        {
            string key = message.AutonomousGroupChatSessionId
                    is Guid sessionId
                ? $"group-session:{sessionId:N}"
                : message.InteractionBatchId is Guid interactionBatchId
                    ? $"group-interaction:{interactionBatchId:N}"
                    : $"group-chat:{message.GroupChatId:N}";
            conversationKeys.Add(key);
        }

        return new AwarenessEvidenceCounts(
            sources.Count,
            conversationKeys.Count);
    }

    private AiWorldAwareness? ReadAwareness(
        Guid observerAiAccountId,
        Guid subjectAiAccountId)
    {
        using VocaChatDbContext dbContext =
            _dbContextFactory.CreateDbContext();
        return dbContext.AiWorldAwareness
            .AsNoTracking()
            .SingleOrDefault(item =>
                item.ObserverAiAccountId == observerAiAccountId
                && item.SubjectAiAccountId == subjectAiAccountId);
    }

    private static AiWorldAwarenessState SelectTargetState(
        AiWorldAwarenessState currentState,
        AiWorldKnowledgeSignal signal,
        int evidenceCount)
    {
        AiWorldAwarenessState suggested = signal switch
        {
            AiWorldKnowledgeSignal.ExplicitCrossWorldConfirmation =>
                AiWorldAwarenessState.CrossWorldConfirmed,
            AiWorldKnowledgeSignal.BackgroundDifference
                when evidenceCount >= 2 =>
                    AiWorldAwarenessState.DifferentBackgroundRecognized,
            AiWorldKnowledgeSignal.BackgroundDifference
                or AiWorldKnowledgeSignal.UnfamiliarConcept =>
                    AiWorldAwarenessState.AnomalyObserved,
            _ => currentState
        };

        return suggested > currentState ? suggested : currentState;
    }

    private static IReadOnlyList<AiAccount> ReadPrivateListeners(
        VocaChatDbContext dbContext,
        PrivateChat privateChat,
        Guid? senderAiAccountId)
    {
        List<Guid> listenerIds = new();

        if (privateChat.Kind == PrivateChatKind.LocalUserAndAiAccount
            && privateChat.ContactId is Guid contactId)
        {
            Guid? aiAccountId = dbContext.Contacts
                .AsNoTracking()
                .Where(contact => contact.Id == contactId)
                .Select(contact => (Guid?)contact.AiAccountId)
                .SingleOrDefault();
            if (aiAccountId.HasValue
                && aiAccountId != senderAiAccountId)
            {
                listenerIds.Add(aiAccountId.Value);
            }
        }
        else if (privateChat.Kind == PrivateChatKind.AiAccounts)
        {
            if (privateChat.FirstAiAccountId is Guid firstId
                && firstId != senderAiAccountId)
            {
                listenerIds.Add(firstId);
            }

            if (privateChat.SecondAiAccountId is Guid secondId
                && secondId != senderAiAccountId)
            {
                listenerIds.Add(secondId);
            }
        }

        return dbContext.AiAccounts
            .AsNoTracking()
            .Where(account => listenerIds.Contains(account.Id))
            .ToList()
            .AsReadOnly();
    }

    private static AiAccount? FindSourceAccount(
        VocaChatDbContext dbContext,
        Guid? senderAiAccountId)
    {
        return senderAiAccountId is not Guid accountId
            ? null
            : dbContext.AiAccounts
                .AsNoTracking()
                .SingleOrDefault(account => account.Id == accountId);
    }

    private static string CreateUserConfirmationKnowledgeKey(
        AiAccount subject,
        string sourceContent)
    {
        byte[] sourceBytes = System.Text.Encoding.UTF8.GetBytes(
            $"{subject.Id:N}:{sourceContent.Trim().ToLowerInvariant()}");
        string hash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(sourceBytes)[..10])
            .ToLowerInvariant();
        return $"user.cross-world-confirmation.{hash}";
    }

    private void RecordFailureIfNeeded(
        AiWorldKnowledgeMessageProcessingResult result,
        AiMessageGenerationScenario scenario,
        Guid? sourceAiAccountId,
        Guid? conversationId)
    {
        if (!result.HasFailures || _diagnosticLogService is null)
        {
            return;
        }

        _diagnosticLogService.TryRecord(
            AiInteractionDiagnosticSeverity.Warning,
            AiInteractionDiagnosticCode.WorldKnowledgeProcessingFailed,
            scenario,
            sourceAiAccountId,
            conversationId,
            "消息已经保存，但世界知识后处理未完全成功。",
            string.Join(" ", result.Errors),
            wasRecovered: true);
    }

    private static string TruncateKnowledgeSummary(string summary)
    {
        return summary.Length <= AiWorldKnowledge.SummaryMaxLength
            ? summary
            : summary[..AiWorldKnowledge.SummaryMaxLength];
    }

    private static AiWorldKnowledgeMessageProcessingResult CreateResult(
        AiWorldKnowledgeMessageProcessingStatus status,
        IEnumerable<Guid>? knowledgeIds = null,
        IEnumerable<Guid>? updatedObservers = null,
        IEnumerable<Guid>? newlyInformed = null,
        IEnumerable<string>? errors = null)
    {
        return new AiWorldKnowledgeMessageProcessingResult(
            status,
            (knowledgeIds ?? Array.Empty<Guid>())
                .Distinct()
                .ToList()
                .AsReadOnly(),
            (updatedObservers ?? Array.Empty<Guid>())
                .Distinct()
                .ToList()
                .AsReadOnly(),
            (newlyInformed ?? Array.Empty<Guid>())
                .Distinct()
                .ToList()
                .AsReadOnly(),
            (errors ?? Array.Empty<string>())
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .Distinct()
                .ToList()
                .AsReadOnly());
    }

    private sealed record AwarenessEvidenceSource(
        Guid? PrivateMessageId,
        Guid? GroupMessageId);

    private sealed record AwarenessEvidenceCounts(
        int EvidenceCount,
        int DistinctConversationCount);
}
