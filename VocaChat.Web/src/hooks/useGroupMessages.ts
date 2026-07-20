import { useCallback, useEffect, useRef, useState } from 'react'
import {
  getGroupMessages,
  getSavedGroupMessages,
  sendGroupMessage,
} from '@/api/groupMessages'
import type { GroupMessageResponse } from '@/api/types'
import type { DisplayChatMessage } from '@/types/displayChatMessage'
import type { MessageSendOutcome } from '@/types/messageSendOutcome'
import type { RemoteStatus } from '@/types/remoteStatus'
import { startInFlightHistorySync } from '@/utils/inFlightHistorySync'

interface GroupMessagesState {
  data: DisplayChatMessage[]
  status: RemoteStatus
  errorMessage?: string
  sendErrorMessage?: string
  isSending: boolean
  reload: () => void
  send: (content: string) => Promise<MessageSendOutcome>
}

/** 管理当前群聊的历史、即时用户气泡、回复同步和部分失败反馈。 */
export function useGroupMessages(groupChatId?: string): GroupMessagesState {
  const [data, setData] = useState<DisplayChatMessage[]>([])
  const [status, setStatus] = useState<RemoteStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>()
  const [sendErrorMessage, setSendErrorMessage] = useState<string>()
  const [isSending, setIsSending] = useState(false)
  const activeGroupChatId = useRef(groupChatId)
  activeGroupChatId.current = groupChatId

  const reload = useCallback(() => {
    setErrorMessage(undefined)
    setSendErrorMessage(undefined)

    if (!groupChatId) {
      setData([])
      setStatus('idle')
      return
    }

    const loadingGroupChatId = groupChatId
    setStatus('loading')

    void getGroupMessages(loadingGroupChatId)
      .then((messages) => {
        if (activeGroupChatId.current !== loadingGroupChatId) return
        setData(sortMessages(messages))
        setStatus('success')
      })
      .catch((error: unknown) => {
        if (activeGroupChatId.current !== loadingGroupChatId) return
        setData([])
        setStatus('error')
        setErrorMessage(
          error instanceof Error ? error.message : '聊天记录加载失败。',
        )
      })
  }, [groupChatId])

  useEffect(() => reload(), [reload])

  const send = useCallback(async (content: string): Promise<MessageSendOutcome> => {
    const normalizedContent = content.trim()

    if (!groupChatId || normalizedContent.length === 0 || isSending) {
      return 'rejected'
    }

    const sendingGroupChatId = groupChatId
    const clientMessageId = crypto.randomUUID()
    const optimisticMessage: DisplayChatMessage = {
      id: clientMessageId,
      senderType: 'User',
      senderDisplayName: '我',
      senderAiAccountId: null,
      sequenceNumber: null,
      senderAvatarUrl: null,
      content: normalizedContent,
      sentAt: new Date().toISOString(),
      deliveryStatus: 'Sending',
    }

    setData((current) => mergeMessages(current, [optimisticMessage]))
    setIsSending(true)
    setSendErrorMessage(undefined)

    const synchronizeHistory = async (): Promise<GroupMessageResponse[]> => {
      try {
        const messages = await getGroupMessages(sendingGroupChatId)
        if (activeGroupChatId.current === sendingGroupChatId) {
          setData((current) => mergeMessages(current, messages))
        }
        return messages
      } catch {
        return []
      }
    }
    const stopHistorySynchronization = startInFlightHistorySync(
      synchronizeHistory,
    )

    try {
      const result = await sendGroupMessage(sendingGroupChatId, {
        clientMessageId,
        content: normalizedContent,
      })

      if (activeGroupChatId.current === sendingGroupChatId) {
        setData((current) => mergeMessages(
          current.filter((message) => message.id !== clientMessageId),
          [result.userMessage, ...result.aiReplies],
        ))
      }

      if (result.replyCompletion === 'Partial') {
        return 'partial'
      }

      return 'success'
    } catch (error: unknown) {
      const savedMessages = getSavedGroupMessages(error)
      const latestHistory = await synchronizeHistory()
      const userMessageWasSaved = savedMessages.some(
        (message) => message.senderType === 'User',
      ) || latestHistory.some((message) => message.id === clientMessageId)

      if (activeGroupChatId.current === sendingGroupChatId) {
        if (savedMessages.length > 0) {
          setData((current) => mergeMessages(current, savedMessages))
        }
        if (!userMessageWasSaved) {
          setData((current) => current.filter(
            (message) => message.id !== clientMessageId,
          ))
        }
        if (!userMessageWasSaved) {
          setSendErrorMessage(
            error instanceof Error ? error.message : '消息发送失败，请重试。',
          )
        }
      }

      return userMessageWasSaved ? 'partial' : 'rejected'
    } finally {
      stopHistorySynchronization()
      if (activeGroupChatId.current === sendingGroupChatId) {
        setIsSending(false)
      }
    }
  }, [groupChatId, isSending])

  return {
    data,
    status,
    errorMessage,
    sendErrorMessage,
    isSending,
    reload,
    send,
  }
}

function mergeMessages(
  current: DisplayChatMessage[],
  incoming: DisplayChatMessage[],
): DisplayChatMessage[] {
  const messagesById = new Map(
    [...current, ...incoming].map((message) => [message.id, message]),
  )

  return sortMessages([...messagesById.values()])
}

function sortMessages(messages: DisplayChatMessage[]): DisplayChatMessage[] {
  return [...messages].sort((left, right) => {
    if (left.sequenceNumber !== null && right.sequenceNumber !== null) {
      return left.sequenceNumber - right.sequenceNumber
    }

    if (left.sequenceNumber === null && right.sequenceNumber !== null) return 1
    if (left.sequenceNumber !== null && right.sequenceNumber === null) return -1

    return left.sentAt.localeCompare(right.sentAt)
      || left.id.localeCompare(right.id)
  })
}
