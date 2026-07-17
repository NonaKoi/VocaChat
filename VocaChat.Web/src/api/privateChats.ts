import { ApiError, getJson, postJson } from '@/api/http'
import type { PrivateMessageResponse, SendPrivateMessageResponse } from '@/api/types'

export function getPrivateMessages(id: string): Promise<PrivateMessageResponse[]> {
  return getJson(`/api/private-chats/${id}/messages`)
}

export function sendPrivateMessage(id: string, content: string): Promise<SendPrivateMessageResponse> {
  return postJson(`/api/private-chats/${id}/messages`, { content })
}

export function getSavedPrivateUserMessage(error: unknown): PrivateMessageResponse | undefined {
  if (!(error instanceof ApiError) || typeof error.responseBody !== 'object' || error.responseBody === null) return undefined
  const value = (error.responseBody as { savedUserMessage?: unknown }).savedUserMessage
  return typeof value === 'object' && value !== null ? value as PrivateMessageResponse : undefined
}
