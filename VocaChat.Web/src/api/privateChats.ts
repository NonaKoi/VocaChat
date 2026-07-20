import { ApiError, getJson, postJson } from '@/api/http'
import type {
  PrivateChatResponse,
  PrivateMessageResponse,
  SendPrivateMessageFailureResponse,
  SendPrivateMessageRequest,
  SendPrivateMessageResponse,
} from '@/api/types'

export function getPrivateChat(id: string): Promise<PrivateChatResponse> {
  return getJson(`/api/private-chats/${id}`)
}

export function getPrivateMessages(id: string): Promise<PrivateMessageResponse[]> {
  return getJson(`/api/private-chats/${id}/messages`)
}

export function sendPrivateMessage(
  id: string,
  request: SendPrivateMessageRequest,
): Promise<SendPrivateMessageResponse> {
  return postJson(`/api/private-chats/${id}/messages`, request)
}

export function getSavedPrivateMessages(error: unknown): PrivateMessageResponse[] {
  if (!(error instanceof ApiError) || !isFailureResponse(error.responseBody)) {
    return []
  }

  return [
    ...(error.responseBody.savedUserMessage
      ? [error.responseBody.savedUserMessage]
      : []),
    ...(error.responseBody.savedAiReplies ?? []),
  ]
}

function isFailureResponse(
  value: unknown,
): value is SendPrivateMessageFailureResponse {
  return typeof value === 'object'
    && value !== null
    && 'message' in value
    && typeof value.message === 'string'
}
