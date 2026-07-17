import { useCallback, useEffect, useState } from 'react'
import { getAiAccounts } from '../api/aiAccounts'
import type { AiAccountResponse } from '../api/types'
import type { RemoteStatus } from '../types/remoteStatus'

interface AiAccountsState {
  data: AiAccountResponse[]
  status: RemoteStatus
  errorMessage?: string
  reload: () => void
}

/** 管理 AI 账号列表的一次加载、错误反馈和手动重试。 */
export function useAiAccounts(): AiAccountsState {
  const [data, setData] = useState<AiAccountResponse[]>([])
  const [status, setStatus] = useState<RemoteStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>()

  const reload = useCallback(() => {
    setStatus('loading')
    setErrorMessage(undefined)

    void getAiAccounts()
      .then((accounts) => {
        setData(accounts)
        setStatus('success')
      })
      .catch((error: unknown) => {
        setStatus('error')
        setErrorMessage(
          error instanceof Error ? error.message : 'AI 账号加载失败。',
        )
      })
  }, [])

  useEffect(() => {
    reload()
  }, [reload])

  return { data, status, errorMessage, reload }
}
