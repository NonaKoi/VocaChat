import { useCallback, useEffect, useState } from 'react'
import { getGroupChats } from '../api/groupChats'
import type { GroupChatResponse } from '../api/types'
import type { RemoteStatus } from '../types/remoteStatus'

interface GroupChatsState {
  data: GroupChatResponse[]
  status: RemoteStatus
  errorMessage?: string
  reload: () => void
}

/** 管理群聊列表的一次加载、错误反馈和手动重试。 */
export function useGroupChats(): GroupChatsState {
  const [data, setData] = useState<GroupChatResponse[]>([])
  const [status, setStatus] = useState<RemoteStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>()

  const reload = useCallback(() => {
    setStatus('loading')
    setErrorMessage(undefined)

    void getGroupChats()
      .then((groupChats) => {
        setData(groupChats)
        setStatus('success')
      })
      .catch((error: unknown) => {
        setStatus('error')
        setErrorMessage(
          error instanceof Error ? error.message : '群聊加载失败。',
        )
      })
  }, [])

  useEffect(() => {
    reload()
  }, [reload])

  return { data, status, errorMessage, reload }
}
