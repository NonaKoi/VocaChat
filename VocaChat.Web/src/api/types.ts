export interface AiAccountResponse {
  id: string
  vcNumber: string
  nickname: string
  identityDescription: string
  personality: string
  speakingStyle: string
  signature: string
  birthday: string | null
  age: number | null
  zodiacSign: string | null
  gender: AiAccountGender
  location: string
  occupation: string
  hometown: string
  onlineStatus: OnlineStatus
  avatarUrl: string | null
  coverUrl: string | null
  characterWorldId: string
  characterWorld: CharacterWorldResponse
  interestTags: string[]
  personalityTags: string[]
  createdAt: string
}

export type AiAccountGender = 'Unspecified' | 'Male' | 'Female' | 'Other'

export type OnlineStatus = 'Offline' | 'Online' | 'Away' | 'Busy'

export interface CreateAiAccountRequest {
  nickname: string
  vcNumber: string
  identityDescription: string
  personality: string
  speakingStyle: string
  signature: string
  birthday: string
  gender: AiAccountGender
  location: string
  occupation: string
  hometown: string
  onlineStatus: OnlineStatus
  characterWorldId?: string
  interestTags: string[]
  personalityTags: string[]
}

export interface UpdateAiAccountRequest {
  nickname: string
  vcNumber: string
  identityDescription: string
  personality: string
  speakingStyle: string
  signature: string
  birthday: string
  gender: AiAccountGender
  location: string
  occupation: string
  hometown: string
  onlineStatus: OnlineStatus
  characterWorldId?: string
  interestTags: string[]
  personalityTags: string[]
}

export interface CharacterWorldResponse {
  id: string
  name: string
  description: string
  createdAt: string
  updatedAt: string
}

export interface CreateCharacterWorldRequest {
  name: string
  description: string
}

export interface UpdateCharacterWorldRequest {
  name: string
  description: string
}

export type AiSelfMemoryType =
  | 'PersonalFact'
  | 'OngoingActivity'
  | 'Plan'
  | 'Experience'
  | 'Preference'

export type AiSelfMemorySource = 'User' | 'Director'

export type AiSelfMemoryStatus = 'Active' | 'Superseded' | 'Archived'

export type AiSelfMemoryFactNature =
  | 'Objective'
  | 'Subjective'
  | 'Narrative'

export type AiSelfMemoryMutability =
  | 'Immutable'
  | 'Mutable'
  | 'Evolving'
  | 'Ephemeral'

export type AiSelfMemoryTrustLevel =
  | 'UserCanon'
  | 'EstablishedCanon'
  | 'NarrativeCandidate'
  | 'SubjectiveState'

export interface AiSelfMemoryResponse {
  id: string
  aiAccountId: string
  type: AiSelfMemoryType
  summary: string
  factKey: string
  factNature: AiSelfMemoryFactNature
  mutability: AiSelfMemoryMutability
  trustLevel: AiSelfMemoryTrustLevel
  characterWorldId: string
  source: AiSelfMemorySource
  status: AiSelfMemoryStatus
  salience: number
  isUserLocked: boolean
  sourceConversationId: string | null
  sourceMessageId: string | null
  supersedesMemoryId: string | null
  occurredAt: string | null
  validFrom: string | null
  validUntil: string | null
  createdAt: string
  updatedAt: string
}

export interface SaveAiSelfMemoryRequest {
  type: AiSelfMemoryType
  summary: string
  factKey: string | null
  factNature: AiSelfMemoryFactNature | null
  mutability: AiSelfMemoryMutability | null
  characterWorldId: string | null
  salience: number
  isUserLocked: boolean
  occurredAt: string | null
  validFrom: string | null
  validUntil: string | null
}

export interface UpdateAiSelfMemoryStatusRequest {
  status: 'Active' | 'Archived'
}

export type AiParallelWorldAwarenessState = 'Unaware' | 'Informed' | 'Accepted'
export type AiWorldAwarenessState =
  | 'AssumedSharedWorld'
  | 'AnomalyObserved'
  | 'DifferentBackgroundRecognized'
  | 'CrossWorldConfirmed'
export type AiWorldFamiliarityLevel =
  | 'Unfamiliar'
  | 'FirstImpression'
  | 'Learning'
  | 'Familiar'
export type AiWorldKnowledgeFactNature =
  | 'ObjectiveStatement'
  | 'SubjectiveView'
  | 'Hearsay'
  | 'Unconfirmed'
export type AiWorldKnowledgeMutability = 'Constant' | 'Changeable' | 'Temporary'
export type AiWorldKnowledgeTrustLevel =
  | 'Unverified'
  | 'DirectStatement'
  | 'Corroborated'
  | 'UserConfirmed'
export type AiWorldKnowledgeStatus =
  | 'Active'
  | 'Superseded'
  | 'Archived'
  | 'ConflictCandidate'

export interface ParallelWorldAwarenessResponse {
  state: AiParallelWorldAwarenessState
  isUserLocked: boolean
  firstInformedAt: string | null
  acceptedAt: string | null
  updatedAt: string | null
}

export interface WorldAwarenessSubjectResponse {
  aiAccountId: string
  nickname: string
  avatarUrl: string | null
  characterWorldId: string
  characterWorldName: string
  awarenessState: AiWorldAwarenessState
  isUserLocked: boolean
  awarenessEvidenceCount: number
  awarenessConversationCount: number
  familiarityLevel: AiWorldFamiliarityLevel
  activeKnowledgeCount: number
  distinctTopicCount: number
  knowledgeEvidenceCount: number
  knowledgeConversationCount: number
}

export interface AiWorldAwarenessOverviewResponse {
  aiAccountId: string
  parallelWorld: ParallelWorldAwarenessResponse
  subjects: WorldAwarenessSubjectResponse[]
}

export interface UpdateAiWorldAwarenessRequest {
  state: AiParallelWorldAwarenessState | AiWorldAwarenessState
  isUserLocked: boolean
}

export interface AiWorldKnowledgeResponse {
  id: string
  ownerAiAccountId: string
  subjectCharacterWorldId: string
  subjectAiAccountId: string | null
  knowledgeKey: string
  summary: string
  factNature: AiWorldKnowledgeFactNature
  mutability: AiWorldKnowledgeMutability
  trustLevel: AiWorldKnowledgeTrustLevel
  status: AiWorldKnowledgeStatus
  salience: number
  isUserLocked: boolean
  evidenceCount: number
  firstLearnedAt: string
  updatedAt: string
}

export interface AiWorldKnowledgeEvidenceResponse {
  evidenceId: string
  sourceType: MessageSenderType
  sourceAiAccountId: string | null
  sourceDisplayName: string
  conversationKind: 'PrivateChat' | 'GroupChat'
  conversationId: string
  conversationDisplayName: string
  messageId: string
  messageContent: string
  sentAt: string
  evidenceSummary: string
}

export interface UpdateAiWorldKnowledgeRequest {
  summary: string
  factNature: AiWorldKnowledgeFactNature
  mutability: AiWorldKnowledgeMutability
  salience: number
  isUserLocked: boolean
  isConfirmed: boolean
}

export interface GroupChatMemberResponse {
  id: string
  nickname: string
  avatarUrl: string | null
}

export interface GroupChatResponse {
  id: string
  name: string
  includesLocalUser: boolean
  createdAt: string
  members: GroupChatMemberResponse[]
}

export interface CreateGroupChatRequest {
  name: string
  memberAiAccountIds: string[]
  includesLocalUser: boolean
}

export interface AddGroupChatMemberRequest {
  aiAccountId: string
}

export type MessageSenderType = 'User' | 'AiAccount'

export interface AiModelStageTokenUsageResponse {
  usageComplete: boolean
  inputTokens: number | null
  outputTokens: number | null
  totalTokens: number | null
  cacheHitTokens: number | null
  cacheMissTokens: number | null
  reasoningTokens: number | null
  attemptCount: number
}

export interface AiMessageTokenUsageResponse {
  groupDirector: AiModelStageTokenUsageResponse | null
  conversationDirector: AiModelStageTokenUsageResponse | null
  replyGeneration: AiModelStageTokenUsageResponse | null
  selfMemoryJudgment: AiModelStageTokenUsageResponse | null
  worldKnowledgeExtraction: AiModelStageTokenUsageResponse | null
  usageComplete: boolean
  totalTokens: number
  interactionSharedMessageCount: number
  responseSharedMessageCount: number
}

export interface GroupMessageResponse {
  id: string
  groupChatId: string
  senderType: MessageSenderType
  senderDisplayName: string
  senderAiAccountId: string | null
  sequenceNumber: number
  interactionBatchId: string | null
  replyToMessageId: string | null
  tokenUsage: AiMessageTokenUsageResponse | null
  senderAvatarUrl: string | null
  content: string
  sentAt: string
}

export interface SendGroupMessageRequest {
  clientMessageId: string
  content: string
}

export interface SendGroupMessageResponse {
  userMessage: GroupMessageResponse
  aiReplies: GroupMessageResponse[]
  replyCompletion: 'Complete' | 'Partial'
  warningMessage: string | null
}

export interface SendGroupMessageFailureResponse {
  message: string
  savedUserMessage?: GroupMessageResponse | null
  savedAiReplies?: GroupMessageResponse[]
}

export interface ContactGroupResponse {
  id: string
  name: string
  sortOrder: number
  createdAt: string
}

export interface ContactResponse {
  id: string
  contactGroupId: string
  contactGroupName: string
  createdAt: string
  friend: AiAccountResponse
}

export type ConversationKind = 'PrivateChat' | 'GroupChat'

export type ConversationCategory =
  | 'MyPrivateChat'
  | 'FriendPrivateChat'
  | 'MyGroupChat'
  | 'FriendGroupChat'

export interface ConversationSummaryResponse {
  id: string
  kind: ConversationKind
  category: ConversationCategory
  displayName: string
  avatarUrl: string | null
  memberCount: number
  contactId: string | null
  latestSenderDisplayName: string | null
  latestMessageContent: string | null
  latestMessageAt: string | null
  createdAt: string
}

export interface PrivateChatResponse {
  id: string
  category: 'MyPrivateChat' | 'FriendPrivateChat'
  contactId: string | null
  friend: AiAccountResponse | null
  participants: AiAccountResponse[]
  createdAt: string
}

export interface ChatMessageResponse {
  id: string
  senderType: MessageSenderType
  senderDisplayName: string
  senderAiAccountId: string | null
  sequenceNumber: number
  tokenUsage: AiMessageTokenUsageResponse | null
  senderAvatarUrl: string | null
  content: string
  sentAt: string
}

export interface PrivateMessageResponse extends ChatMessageResponse {
  privateChatId: string
}

export interface SendPrivateMessageRequest {
  clientMessageId: string
  content: string
}

export interface SendPrivateMessageResponse {
  userMessage: PrivateMessageResponse
  aiReplies: PrivateMessageResponse[]
  replyCompletion: 'Complete' | 'Partial'
  warningMessage: string | null
}

export interface SendPrivateMessageFailureResponse {
  message: string
  savedUserMessage?: PrivateMessageResponse | null
  savedAiReplies?: PrivateMessageResponse[]
}

export interface PostImageResponse {
  id: string
  imageUrl: string
  sortOrder: number
}

export interface PostCommentSummaryResponse {
  id: string
  senderDisplayName: string
  content: string
  createdAt: string
}

export interface PostResponse {
  id: string
  authorAiAccountId: string
  authorNickname: string
  authorAvatarUrl: string | null
  content: string
  createdAt: string
  images: PostImageResponse[]
  likeCount: number
  commentCount: number
  isLikedByLocalUser: boolean
  recentComments: PostCommentSummaryResponse[]
}

export type AutonomyLevel = 'Low' | 'Normal' | 'High'
export type AutonomousInteractionFrequency = AutonomyLevel
export type AutonomousInteractionInitiativeLevel = AutonomyLevel
export type AiReplyDelayMode = 'Fixed' | 'RandomRange'

export interface AutonomousInteractionSettingsResponse {
  isEnabled: boolean
  frequency: AutonomousInteractionFrequency
  allowPrivateChats: boolean
  allowGroupChats: boolean
  privateChatContinuationRatePercent: number
  privateChatMaximumRounds: number
  autonomousGroupChatMaximumMembers: number
  groupChatContinuationRatePercent: number
  groupChatMaximumRounds: number
  replyDelayMode: AiReplyDelayMode
  fixedReplyDelayMilliseconds: number
  minimumReplyDelayMilliseconds: number
  maximumReplyDelayMilliseconds: number
  consecutiveMessageDelayMode: AiReplyDelayMode
  fixedConsecutiveMessageDelayMilliseconds: number
  minimumConsecutiveMessageDelayMilliseconds: number
  maximumConsecutiveMessageDelayMilliseconds: number
  maximumConsecutiveQuestionTurns: number
  minimumReplyMessageCount: number
  maximumReplyMessageCount: number
  groupChatMaximumSpeakersPerTurn: number
  groupChatWholeGroupMaximumSpeakersPerTurn: number
  groupChatMaximumMessagesPerTurn: number
}

export interface UpdateAutonomousInteractionSettingsRequest {
  isEnabled: boolean
  frequency: AutonomousInteractionFrequency
  allowPrivateChats: boolean
  allowGroupChats: boolean
  privateChatContinuationRatePercent: number
  privateChatMaximumRounds: number
  autonomousGroupChatMaximumMembers: number
  groupChatContinuationRatePercent: number
  groupChatMaximumRounds: number
  replyDelayMode: AiReplyDelayMode
  fixedReplyDelayMilliseconds: number
  minimumReplyDelayMilliseconds: number
  maximumReplyDelayMilliseconds: number
  consecutiveMessageDelayMode: AiReplyDelayMode
  fixedConsecutiveMessageDelayMilliseconds: number
  minimumConsecutiveMessageDelayMilliseconds: number
  maximumConsecutiveMessageDelayMilliseconds: number
  maximumConsecutiveQuestionTurns: number
  minimumReplyMessageCount: number
  maximumReplyMessageCount: number
  groupChatMaximumSpeakersPerTurn: number
  groupChatWholeGroupMaximumSpeakersPerTurn: number
  groupChatMaximumMessagesPerTurn: number
}

export interface AiAccountAutonomySettingsResponse {
  aiAccountId: string
  isEnabled: boolean
  initiativeLevel: AutonomousInteractionInitiativeLevel
  canInitiatePrivateChats: boolean
  canInitiateGroupChats: boolean
  canJoinGroupChats: boolean
  useGlobalReplyDelay: boolean
  replyDelayMode: AiReplyDelayMode
  fixedReplyDelayMilliseconds: number
  minimumReplyDelayMilliseconds: number
  maximumReplyDelayMilliseconds: number
  useGlobalConsecutiveMessageDelay: boolean
  consecutiveMessageDelayMode: AiReplyDelayMode
  fixedConsecutiveMessageDelayMilliseconds: number
  minimumConsecutiveMessageDelayMilliseconds: number
  maximumConsecutiveMessageDelayMilliseconds: number
  useGlobalQuestionPolicy: boolean
  maximumConsecutiveQuestionTurns: number
  useGlobalReplyMessageCount: boolean
  minimumReplyMessageCount: number
  maximumReplyMessageCount: number
}

export interface UpdateAiAccountAutonomySettingsRequest {
  isEnabled: boolean
  initiativeLevel: AutonomousInteractionInitiativeLevel
  canInitiatePrivateChats: boolean
  canInitiateGroupChats: boolean
  canJoinGroupChats: boolean
  useGlobalReplyDelay: boolean
  replyDelayMode: AiReplyDelayMode
  fixedReplyDelayMilliseconds: number
  minimumReplyDelayMilliseconds: number
  maximumReplyDelayMilliseconds: number
  useGlobalConsecutiveMessageDelay: boolean
  consecutiveMessageDelayMode: AiReplyDelayMode
  fixedConsecutiveMessageDelayMilliseconds: number
  minimumConsecutiveMessageDelayMilliseconds: number
  maximumConsecutiveMessageDelayMilliseconds: number
  useGlobalQuestionPolicy: boolean
  maximumConsecutiveQuestionTurns: number
  useGlobalReplyMessageCount: boolean
  minimumReplyMessageCount: number
  maximumReplyMessageCount: number
}

export interface AiModelConnectionSettingsResponse {
  baseUrl: string
  model: string
  hasApiKey: boolean
}

export interface UpdateAiModelConnectionSettingsRequest {
  baseUrl: string
  model: string
  apiKey: string
  clearApiKey: boolean
}

export interface AiAccountModelConnectionSettingsResponse {
  aiAccountId: string
  useGlobalSettings: boolean
  baseUrl: string
  model: string
  hasApiKey: boolean
  effectiveBaseUrl: string
  effectiveModel: string
  effectiveHasApiKey: boolean
}

export interface UpdateAiAccountModelConnectionSettingsRequest {
  useGlobalSettings: boolean
  baseUrl: string
  model: string
  apiKey: string
  clearApiKey: boolean
}

export interface AiInteractionDiagnosticLogResponse {
  id: string
  occurredAt: string
  severity: 'Information' | 'Warning' | 'Error'
  code:
    | 'MessageGenerationFailed'
    | 'MessagePersistenceFailed'
    | 'ReplyTimingFailed'
    | 'SelfMemoryDecision'
    | 'SelfMemoryPersistenceFailed'
    | 'GroupConversationPlanCreated'
    | 'GroupConversationPlanFallback'
    | 'GroupConversationExecutionFailed'
  scenario: string
  aiAccountId: string | null
  conversationId: string | null
  summary: string
  detail: string
  wasRecovered: boolean
}

export interface AiRelationshipResponse {
  fromAiAccountId: string
  toAiAccountId: string
  familiarity: number
  affinity: number
  trust: number
  interactionCount: number
  lastInteractionAt: string | null
  updatedAt: string | null
}

export interface UpdateAiRelationshipRequest {
  familiarity: number
  affinity: number
  trust: number
}

export type AutonomousPrivateChatDecisionStage =
  | 'Approved'
  | 'SelfInteractionNotAllowed'
  | 'AccountNotFound'
  | 'GlobalDisabled'
  | 'PrivateChatsDisabled'
  | 'ParticipantDisabled'
  | 'NoEligibleInitiator'
  | 'CooldownActive'
  | 'ScoreBelowThreshold'

export interface EvaluateAutonomousPrivateChatRequest {
  firstAiAccountId: string
  secondAiAccountId: string
}

export interface AutonomousPrivateChatDecisionResponse {
  isApproved: boolean
  stage: AutonomousPrivateChatDecisionStage
  interactionType: 'PrivateChat'
  firstAiAccountId: string
  secondAiAccountId: string
  initiatorAiAccountId: string | null
  recipientAiAccountId: string | null
  relationshipScore: number
  initiativeAdjustment: number
  randomJitter: number
  finalScore: number
  threshold: number
  cooldownEndsAt: string | null
}

export type AutonomousPrivateChatExecutionStatus =
  | 'Completed'
  | 'DecisionRejected'
  | 'ChatCreationFailed'
  | 'SessionCreationFailed'
  | 'PlanningFailed'
  | 'GenerationFailed'
  | 'MessagePersistenceFailed'
  | 'RelationshipRecordFailed'
  | 'RelationshipEvolutionFailed'
  | 'SessionFinalizationFailed'

export type AutonomousPrivateChatSessionStatus =
  | 'Running'
  | 'Completed'
  | 'Failed'
  | 'Cancelled'

export type AutonomousPrivateChatSessionEndReason =
  | 'NaturalConclusion'
  | 'PlannedLimitReached'
  | 'HardLimitReached'
  | 'ParticipantUnavailable'
  | 'InteractionDisabled'
  | 'GenerationFailed'
  | 'MessagePersistenceFailed'
  | 'RelationshipUpdateFailed'
  | 'CancelledByUser'
  | 'ContinuationProbabilityDeclined'

export interface AutonomousPrivateChatSessionResponse {
  id: string
  privateChatId: string
  initiatorAiAccountId: string
  recipientAiAccountId: string
  topic: string
  maximumRounds: number
  continuationRatePercent: number
  completedRounds: number
  status: AutonomousPrivateChatSessionStatus
  endReason: AutonomousPrivateChatSessionEndReason | null
  startedAt: string
  lastActivityAt: string
  endedAt: string | null
}

export interface RunAutonomousPrivateChatRequest {
  firstAiAccountId: string
  secondAiAccountId: string
  topic?: string
}

export type AutonomousPrivateChatMessageMode = 'None' | 'Single' | 'Burst'

export interface AutonomousPrivateChatRoundResponse {
  id: string
  roundNumber: number
  isClosing: boolean
  occurrenceProbability: number | null
  randomRoll: number | null
  initiatorMessageMode: AutonomousPrivateChatMessageMode
  recipientMessageMode: AutonomousPrivateChatMessageMode
  initiatorMessageCount: number
  recipientMessageCount: number
  startedAt: string
  completedAt: string | null
}

export interface AutonomousPrivateChatExecutionResponse {
  status: AutonomousPrivateChatExecutionStatus
  decision: AutonomousPrivateChatDecisionResponse
  privateChat: PrivateChatResponse | null
  privateChatCreated: boolean
  session: AutonomousPrivateChatSessionResponse | null
  rounds: AutonomousPrivateChatRoundResponse[]
  messages: PrivateMessageResponse[]
  errorMessage: string | null
}

export type AutonomousGroupChatDecisionStage =
  | 'Approved'
  | 'TooFewParticipants'
  | 'TooManyParticipants'
  | 'DuplicateParticipant'
  | 'AccountNotFound'
  | 'GlobalDisabled'
  | 'GroupChatsDisabled'
  | 'ParticipantDisabled'
  | 'ParticipantCannotJoin'
  | 'NoEligibleInitiator'
  | 'ScoreBelowThreshold'

export interface EvaluateAutonomousGroupChatRequest {
  participantAiAccountIds: string[]
}

export interface RunAutonomousGroupChatRequest {
  participantAiAccountIds: string[]
  topic?: string
}

export interface AutonomousGroupChatDecisionResponse {
  isApproved: boolean
  stage: AutonomousGroupChatDecisionStage
  participantAiAccountIds: string[]
  initiatorAiAccountId: string | null
  maximumMembers: number
  averageRelationshipScore: number
  weakestRelationshipScore: number
  sharedInterestBonus: number
  initiativeAdjustment: number
  randomJitter: number
  finalScore: number
  threshold: number
}

export type AutonomousGroupChatExecutionStatus =
  | 'Completed'
  | 'DecisionRejected'
  | 'PlanningFailed'
  | 'GroupChatCreationFailed'
  | 'SessionCreationFailed'
  | 'ParticipantUnavailable'
  | 'GenerationFailed'
  | 'MessagePersistenceFailed'
  | 'SessionFinalizationFailed'

export interface AutonomousGroupChatSessionResponse {
  id: string
  groupChatId: string
  initiatorAiAccountId: string
  topic: string
  participantAiAccountIds: string[]
  maximumRounds: number
  continuationRatePercent: number
  completedRounds: number
  status: 'Running' | 'Completed' | 'Failed'
  endReason:
    | 'Completed'
    | 'ParticipantUnavailable'
    | 'GenerationFailed'
    | 'MessagePersistenceFailed'
    | 'NaturalConclusion'
    | 'ContinuationProbabilityDeclined'
    | 'HardLimitReached'
    | null
  startedAt: string
  lastActivityAt: string
  endedAt: string | null
}

export interface AutonomousGroupChatRoundResponse {
  id: string
  roundNumber: number
  isClosing: boolean
  occurrenceProbability: number | null
  randomRoll: number | null
  plannedSpeakerCount: number
  plannedMessageCount: number
  startedAt: string
  completedAt: string | null
}

export interface AutonomousGroupChatExecutionResponse {
  status: AutonomousGroupChatExecutionStatus
  decision: AutonomousGroupChatDecisionResponse
  groupChat: GroupChatResponse | null
  groupChatCreated: boolean
  session: AutonomousGroupChatSessionResponse | null
  rounds: AutonomousGroupChatRoundResponse[]
  messages: GroupMessageResponse[]
  errorMessage: string | null
}
