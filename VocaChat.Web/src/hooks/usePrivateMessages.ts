import { useCallback, useEffect, useRef, useState } from 'react'
import {
  getPrivateChat,
  getPrivateMessages,
  getSavedPrivateMessages,
  sendPrivateMessage,
} from '@/api/privateChats'
import type { PrivateChatResponse, PrivateMessageResponse } from '@/api/types'
import type { DisplayChatMessage } from '@/types/displayChatMessage'
import type { MessageSendOutcome } from '@/types/messageSendOutcome'
import type { RemoteStatus } from '@/types/remoteStatus'
import { startInFlightHistorySync } from '@/utils/inFlightHistorySync'

export function usePrivateMessages(privateChatId?: string) {
  const [data, setData] = useState<DisplayChatMessage[]>([])
  const [privateChat, setPrivateChat] = useState<PrivateChatResponse>()
  const [status, setStatus] = useState<RemoteStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>()
  const [sendErrorMessage, setSendErrorMessage] = useState<string>()
  const [isSending, setIsSending] = useState(false)
  const activePrivateChatId = useRef(privateChatId)
  activePrivateChatId.current = privateChatId

  const reload = useCallback(() => {
    if (!privateChatId) {
      setData([])
      setPrivateChat(undefined)
      setStatus('idle')
      return
    }

    const loadingPrivateChatId = privateChatId
    setPrivateChat(undefined)
    setStatus('loading')
    setErrorMessage(undefined)

    void Promise.all([
      getPrivateChat(loadingPrivateChatId),
      getPrivateMessages(loadingPrivateChatId),
    ]).then(([loadedPrivateChat, messages]) => {
      if (activePrivateChatId.current !== loadingPrivateChatId) return
      setPrivateChat(loadedPrivateChat)
      setData(sort(messages))
      setStatus('success')
    }).catch((error: unknown) => {
      if (activePrivateChatId.current !== loadingPrivateChatId) return
      setStatus('error')
      setErrorMessage(
        error instanceof Error ? error.message : '私聊记录加载失败。',
      )
    })
  }, [privateChatId])

  useEffect(() => reload(), [reload])

  const send = useCallback(async (content: string): Promise<MessageSendOutcome> => {
    const normalizedContent = content.trim()
    if (!privateChatId || !normalizedContent || isSending) return 'rejected'

    const sendingPrivateChatId = privateChatId
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

    setData((current) => merge(current, [optimisticMessage]))
    setIsSending(true)
    setSendErrorMessage(undefined)

    const synchronizeHistory = async (): Promise<PrivateMessageResponse[]> => {
      try {
        const messages = await getPrivateMessages(sendingPrivateChatId)
        if (activePrivateChatId.current === sendingPrivateChatId) {
          setData((current) => merge(current, messages))
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
      const result = await sendPrivateMessage(sendingPrivateChatId, {
        clientMessageId,
        content: normalizedContent,
      })
      if (activePrivateChatId.current === sendingPrivateChatId) {
        setData((current) => merge(
          current.filter((message) => message.id !== clientMessageId),
          [result.userMessage, ...result.aiReplies],
        ))
      }

      if (result.replyCompletion === 'Partial') {
        return 'partial'
      }
      return 'success'
    } catch (error: unknown) {
      const savedMessages = getSavedPrivateMessages(error)
      const latestHistory = await synchronizeHistory()
      const userMessageWasSaved = savedMessages.some(
        (message) => message.senderType === 'User',
      ) || latestHistory.some((message) => message.id === clientMessageId)

      if (activePrivateChatId.current === sendingPrivateChatId) {
        if (savedMessages.length > 0) {
          setData((current) => merge(current, savedMessages))
        }
        if (!userMessageWasSaved) {
          setData((current) => current.filter(
            (message) => message.id !== clientMessageId,
          ))
        }
        if (!userMessageWasSaved) {
          setSendErrorMessage(
            error instanceof Error ? error.message : '消息发送失败。',
          )
        }
      }

      return userMessageWasSaved ? 'partial' : 'rejected'
    } finally {
      stopHistorySynchronization()
      if (activePrivateChatId.current === sendingPrivateChatId) {
        setIsSending(false)
      }
    }
  }, [privateChatId, isSending])

  return {
    data,
    privateChat,
    status,
    errorMessage,
    sendErrorMessage,
    isSending,
    reload,
    send,
  }
}

function merge(
  current: DisplayChatMessage[],
  incoming: DisplayChatMessage[],
): DisplayChatMessage[] {
  return sort([
    ...new Map([...current, ...incoming].map((item) => [item.id, item])).values(),
  ])
}

function sort(items: DisplayChatMessage[]): DisplayChatMessage[] {
  return [...items].sort(compareMessages)
}

function compareMessages(
  left: DisplayChatMessage,
  right: DisplayChatMessage,
): number {
  if (left.sequenceNumber !== null && right.sequenceNumber !== null) {
    return left.sequenceNumber - right.sequenceNumber
  }

  if (left.sequenceNumber === null && right.sequenceNumber !== null) return 1
  if (left.sequenceNumber !== null && right.sequenceNumber === null) return -1

  return left.sentAt.localeCompare(right.sentAt)
    || left.id.localeCompare(right.id)
}
