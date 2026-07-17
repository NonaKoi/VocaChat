import { ApiError, getJson, postJson } from './http'
import type {
  GroupMessageResponse,
  SendGroupMessageFailureResponse,
  SendGroupMessageRequest,
  SendGroupMessageResponse,
} from './types'

/** 返回指定群聊的完整消息历史。 */
export function getGroupMessages(
  groupChatId: string,
): Promise<GroupMessageResponse[]> {
  return getJson<GroupMessageResponse[]>(
    `/api/group-chats/${groupChatId}/messages`,
  )
}

/** 保存用户消息，并返回本轮保存的用户消息和模拟 AI 回复。 */
export function sendGroupMessage(
  groupChatId: string,
  request: SendGroupMessageRequest,
): Promise<SendGroupMessageResponse> {
  return postJson<SendGroupMessageResponse, SendGroupMessageRequest>(
    `/api/group-chats/${groupChatId}/messages`,
    request,
  )
}

/** 从部分失败响应中取得已经持久化的用户消息。 */
export function getSavedUserMessage(
  error: unknown,
): GroupMessageResponse | undefined {
  if (!(error instanceof ApiError) || !isFailureResponse(error.responseBody)) {
    return undefined
  }

  return error.responseBody.savedUserMessage ?? undefined
}

function isFailureResponse(
  value: unknown,
): value is SendGroupMessageFailureResponse {
  return (
    typeof value === 'object' &&
    value !== null &&
    'message' in value &&
    typeof value.message === 'string'
  )
}
