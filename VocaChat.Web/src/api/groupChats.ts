import { getJson, postJson } from './http'
import type {
  AddGroupChatMemberRequest,
  CreateGroupChatRequest,
  GroupChatResponse,
} from './types'

/** 返回全部群聊及其当前 AI 成员摘要。 */
export function getGroupChats(): Promise<GroupChatResponse[]> {
  return getJson<GroupChatResponse[]>('/api/group-chats')
}

/** 使用已有好友创建一个包含本地用户或仅好友参与的群聊。 */
export function createGroupChat(
  request: CreateGroupChatRequest,
): Promise<GroupChatResponse> {
  return postJson<GroupChatResponse, CreateGroupChatRequest>(
    '/api/group-chats',
    request,
  )
}

/** 将一个已有好友加入指定群聊。 */
export function addGroupChatMember(
  groupChatId: string,
  request: AddGroupChatMemberRequest,
): Promise<GroupChatResponse> {
  return postJson<GroupChatResponse, AddGroupChatMemberRequest>(
    `/api/group-chats/${groupChatId}/members`,
    request,
  )
}
