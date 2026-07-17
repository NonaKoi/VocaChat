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
  aiReply: GroupMessageResponse
}

export interface SendGroupMessageFailureResponse {
  message: string
  savedUserMessage?: GroupMessageResponse | null
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
  aiReply: PrivateMessageResponse
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
}

export interface UpdateAutonomousInteractionSettingsRequest {
  isEnabled: boolean
  frequency: AutonomousInteractionFrequency
  allowPrivateChats: boolean
  allowGroupChats: boolean
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
