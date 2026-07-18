import { useCallback, useEffect, useState } from 'react'
import {
  addGroupChatMember,
  createGroupChat,
  getGroupChats,
} from '../api/groupChats'
import type {
  CreateGroupChatRequest,
  GroupChatResponse,
} from '../api/types'
import type { RemoteStatus } from '../types/remoteStatus'

interface GroupChatsState {
  data: GroupChatResponse[]
  status: RemoteStatus
  errorMessage?: string
  isCreating: boolean
  addingMemberToGroupId?: string
  createErrorMessage?: string
  memberErrorMessage?: string
  reload: () => Promise<GroupChatResponse[] | undefined>
  create: (request: CreateGroupChatRequest) => Promise<GroupChatResponse | undefined>
  addMember: (groupChatId: string, aiAccountId: string) => Promise<GroupChatResponse | undefined>
  clearCreateError: () => void
  clearMemberError: () => void
}

/** 管理群聊读取、创建和成员添加，并让数据库响应成为前端列表的最新状态。 */
export function useGroupChats(): GroupChatsState {
  const [data, setData] = useState<GroupChatResponse[]>([])
  const [status, setStatus] = useState<RemoteStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>()
  const [isCreating, setIsCreating] = useState(false)
  const [addingMemberToGroupId, setAddingMemberToGroupId] = useState<string>()
  const [createErrorMessage, setCreateErrorMessage] = useState<string>()
  const [memberErrorMessage, setMemberErrorMessage] = useState<string>()

  const reload = useCallback(async () => {
    setStatus('loading')
    setErrorMessage(undefined)

    try {
      const groupChats = await getGroupChats()
      setData(groupChats)
      setStatus('success')
      return groupChats
    } catch (error: unknown) {
      setStatus('error')
      setErrorMessage(
        error instanceof Error ? error.message : '群聊加载失败。',
      )
      return undefined
    }
  }, [])

  useEffect(() => {
    void reload()
  }, [reload])

  const create = useCallback(async (request: CreateGroupChatRequest) => {
    setIsCreating(true)
    setCreateErrorMessage(undefined)

    try {
      const createdGroupChat = await createGroupChat(request)
      setData((current) => [...current, createdGroupChat])
      return createdGroupChat
    } catch (error: unknown) {
      setCreateErrorMessage(
        error instanceof Error ? error.message : '群聊创建失败。',
      )
      return undefined
    } finally {
      setIsCreating(false)
    }
  }, [])

  const addMember = useCallback(async (
    groupChatId: string,
    aiAccountId: string,
  ) => {
    setAddingMemberToGroupId(groupChatId)
    setMemberErrorMessage(undefined)

    try {
      const updatedGroupChat = await addGroupChatMember(groupChatId, {
        aiAccountId,
      })
      setData((current) => current.map((groupChat) => (
        groupChat.id === groupChatId ? updatedGroupChat : groupChat
      )))
      return updatedGroupChat
    } catch (error: unknown) {
      setMemberErrorMessage(
        error instanceof Error ? error.message : '群成员添加失败。',
      )
      return undefined
    } finally {
      setAddingMemberToGroupId(undefined)
    }
  }, [])

  return {
    data,
    status,
    errorMessage,
    isCreating,
    addingMemberToGroupId,
    createErrorMessage,
    memberErrorMessage,
    reload,
    create,
    addMember,
    clearCreateError: () => setCreateErrorMessage(undefined),
    clearMemberError: () => setMemberErrorMessage(undefined),
  }
}
