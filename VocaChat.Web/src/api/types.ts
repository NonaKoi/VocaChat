export interface AiAccountResponse {
  id: string
  nickname: string
  identityDescription: string
  personality: string
  speakingStyle: string
  createdAt: string
}

export interface GroupChatMemberResponse {
  id: string
  nickname: string
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
