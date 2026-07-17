import { getJson } from './http'
import type { GroupChatResponse } from './types'

/** 返回全部群聊及其当前 AI 成员摘要。 */
export function getGroupChats(): Promise<GroupChatResponse[]> {
  return getJson<GroupChatResponse[]>('/api/group-chats')
}
