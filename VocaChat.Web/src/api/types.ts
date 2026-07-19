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
  interestTags: string[]
  personalityTags: string[]
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

export interface GroupMessageResponse {
  id: string
  groupChatId: string
  senderType: MessageSenderType
  senderDisplayName: string
  senderAiAccountId: string | null
  senderAvatarUrl: string | null
  content: string
  sentAt: string
}

export interface SendGroupMessageRequest {
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
  senderAvatarUrl: string | null
  content: string
  sentAt: string
}

export interface PrivateMessageResponse extends ChatMessageResponse {
  privateChatId: string
}

export interface SendPrivateMessageResponse {
  userMessage: PrivateMessageResponse
  aiReplies: PrivateMessageResponse[]
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

export interface AutonomousInteractionSettingsResponse {
  isEnabled: boolean
  frequency: AutonomousInteractionFrequency
  allowPrivateChats: boolean
  allowGroupChats: boolean
  privateChatContinuationRatePercent: number
  privateChatMaximumRounds: number
  autonomousGroupChatMaximumMembers: number
}

export interface UpdateAutonomousInteractionSettingsRequest {
  isEnabled: boolean
  frequency: AutonomousInteractionFrequency
  allowPrivateChats: boolean
  allowGroupChats: boolean
  privateChatContinuationRatePercent: number
  privateChatMaximumRounds: number
  autonomousGroupChatMaximumMembers: number
}

export interface AiAccountAutonomySettingsResponse {
  aiAccountId: string
  isEnabled: boolean
  initiativeLevel: AutonomousInteractionInitiativeLevel
  canInitiatePrivateChats: boolean
  canInitiateGroupChats: boolean
  canJoinGroupChats: boolean
}

export interface UpdateAiAccountAutonomySettingsRequest {
  isEnabled: boolean
  initiativeLevel: AutonomousInteractionInitiativeLevel
  canInitiatePrivateChats: boolean
  canInitiateGroupChats: boolean
  canJoinGroupChats: boolean
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
  status: 'Running' | 'Completed' | 'Failed'
  endReason:
    | 'Completed'
    | 'ParticipantUnavailable'
    | 'GenerationFailed'
    | 'MessagePersistenceFailed'
    | null
  startedAt: string
  lastActivityAt: string
  endedAt: string | null
}

export interface AutonomousGroupChatExecutionResponse {
  status: AutonomousGroupChatExecutionStatus
  decision: AutonomousGroupChatDecisionResponse
  groupChat: GroupChatResponse | null
  groupChatCreated: boolean
  session: AutonomousGroupChatSessionResponse | null
  messages: GroupMessageResponse[]
  errorMessage: string | null
}
