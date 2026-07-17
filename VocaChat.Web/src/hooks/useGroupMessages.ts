import { useCallback, useEffect, useState } from 'react'
import {
  getGroupMessages,
  getSavedUserMessage,
  sendGroupMessage,
} from '@/api/groupMessages'
import type { GroupMessageResponse } from '@/api/types'
import type { RemoteStatus } from '@/types/remoteStatus'

interface GroupMessagesState {
  data: GroupMessageResponse[]
  status: RemoteStatus
  errorMessage?: string
  sendErrorMessage?: string
  isSending: boolean
  reload: () => void
  send: (content: string) => Promise<MessageSendOutcome>
}

export type MessageSendOutcome = 'success' | 'rejected' | 'partial'

/** 管理当前群聊的历史读取、消息发送和部分失败反馈。 */
export function useGroupMessages(
  groupChatId?: string,
): GroupMessagesState {
  const [data, setData] = useState<GroupMessageResponse[]>([])
  const [status, setStatus] = useState<RemoteStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>()
  const [sendErrorMessage, setSendErrorMessage] = useState<string>()
  const [isSending, setIsSending] = useState(false)

  const reload = useCallback(() => {
    setErrorMessage(undefined)
    setSendErrorMessage(undefined)

    if (!groupChatId) {
      setData([])
      setStatus('idle')
      return
    }

    setStatus('loading')

    void getGroupMessages(groupChatId)
      .then((messages) => {
        setData(sortMessages(messages))
        setStatus('success')
      })
      .catch((error: unknown) => {
        setData([])
        setStatus('error')
        setErrorMessage(
          error instanceof Error ? error.message : '聊天记录加载失败。',
        )
      })
  }, [groupChatId])

  useEffect(() => {
    reload()
  }, [reload])

  const send = useCallback(
    async (content: string): Promise<MessageSendOutcome> => {
      const normalizedContent = content.trim()

      if (!groupChatId || normalizedContent.length === 0 || isSending) {
        return 'rejected'
      }

      setIsSending(true)
      setSendErrorMessage(undefined)

      try {
        const result = await sendGroupMessage(groupChatId, {
          content: normalizedContent,
        })

        setData((current) =>
          mergeMessages(current, [result.userMessage, result.aiReply]),
        )
        return 'success'
      } catch (error: unknown) {
        const savedUserMessage = getSavedUserMessage(error)

        if (savedUserMessage) {
          setData((current) => mergeMessages(current, [savedUserMessage]))
        }

        setSendErrorMessage(
          error instanceof Error ? error.message : '消息发送失败，请重试。',
        )
        return savedUserMessage ? 'partial' : 'rejected'
      } finally {
        setIsSending(false)
      }
    },
    [groupChatId, isSending],
  )

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
  current: GroupMessageResponse[],
  incoming: GroupMessageResponse[],
): GroupMessageResponse[] {
  const messagesById = new Map(
    [...current, ...incoming].map((message) => [message.id, message]),
  )

  return sortMessages([...messagesById.values()])
}

function sortMessages(
  messages: GroupMessageResponse[],
): GroupMessageResponse[] {
  return [...messages].sort((left, right) => {
    const timeDifference =
      new Date(left.sentAt).getTime() - new Date(right.sentAt).getTime()

    return timeDifference === 0
      ? left.id.localeCompare(right.id)
      : timeDifference
  })
}
