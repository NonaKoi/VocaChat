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

export interface ConversationSummaryResponse {
  id: string
  kind: ConversationKind
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
  contactId: string
  friend: AiAccountResponse
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
