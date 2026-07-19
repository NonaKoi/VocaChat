import { useCallback, useEffect, useState } from 'react'
import {
  getPrivateChat,
  getPrivateMessages,
  getSavedPrivateUserMessage,
  sendPrivateMessage,
} from '@/api/privateChats'
import type { PrivateChatResponse, PrivateMessageResponse } from '@/api/types'
import type { MessageSendOutcome } from '@/types/messageSendOutcome'
import type { RemoteStatus } from '@/types/remoteStatus'

export function usePrivateMessages(privateChatId?: string) {
  const [data, setData] = useState<PrivateMessageResponse[]>([])
  const [privateChat, setPrivateChat] = useState<PrivateChatResponse>()
  const [status, setStatus] = useState<RemoteStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>()
  const [sendErrorMessage, setSendErrorMessage] = useState<string>()
  const [isSending, setIsSending] = useState(false)
  const reload = useCallback(() => {
    if (!privateChatId) {
      setData([])
      setPrivateChat(undefined)
      setStatus('idle')
      return
    }
    setPrivateChat(undefined)
    setStatus('loading')
    setErrorMessage(undefined)
    void Promise.all([
      getPrivateChat(privateChatId),
      getPrivateMessages(privateChatId),
    ]).then(([loadedPrivateChat, messages]) => {
      setPrivateChat(loadedPrivateChat)
      setData(sort(messages))
      setStatus('success')
    }).catch((error: unknown) => {
      setStatus('error'); setErrorMessage(error instanceof Error ? error.message : '私聊记录加载失败。')
    })
  }, [privateChatId])
  useEffect(() => reload(), [reload])
  const send = useCallback(async (content: string): Promise<MessageSendOutcome> => {
    const normalized = content.trim()
    if (!privateChatId || !normalized || isSending) return 'rejected'
    setIsSending(true); setSendErrorMessage(undefined)
    try {
      const result = await sendPrivateMessage(privateChatId, normalized)
      setData((current) => merge(current, [result.userMessage, result.aiReply]))
      return 'success'
    } catch (error) {
      const saved = getSavedPrivateUserMessage(error)
      if (saved) setData((current) => merge(current, [saved]))
      setSendErrorMessage(error instanceof Error ? error.message : '消息发送失败。')
      return saved ? 'partial' : 'rejected'
    } finally { setIsSending(false) }
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

function merge(current: PrivateMessageResponse[], incoming: PrivateMessageResponse[]) {
  return sort([...new Map([...current, ...incoming].map((item) => [item.id, item])).values()])
}
function sort(items: PrivateMessageResponse[]) {
  return [...items].sort((a, b) => new Date(a.sentAt).getTime() - new Date(b.sentAt).getTime() || a.id.localeCompare(b.id))
}
