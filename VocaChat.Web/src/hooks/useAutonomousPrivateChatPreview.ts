import { useCallback, useEffect, useRef, useState } from 'react'
import { evaluateAutonomousPrivateChat } from '@/api/autonomousInteractions'
import type { AutonomousPrivateChatDecisionResponse } from '@/api/types'
import type { RemoteStatus } from '@/types/remoteStatus'

/** 管理一对好友的只读自主私信判断，不创建会话或消息。 */
export function useAutonomousPrivateChatPreview(
  firstAiAccountId?: string,
  secondAiAccountId?: string,
) {
  const [data, setData] = useState<AutonomousPrivateChatDecisionResponse>()
  const [status, setStatus] = useState<RemoteStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>()
  const requestVersion = useRef(0)

  useEffect(() => {
    requestVersion.current += 1
    setData(undefined)
    setStatus('idle')
    setErrorMessage(undefined)
  }, [firstAiAccountId, secondAiAccountId])

  const evaluate = useCallback(async () => {
    if (!firstAiAccountId || !secondAiAccountId) return

    const currentVersion = ++requestVersion.current
    setStatus('loading')
    setErrorMessage(undefined)

    try {
      const decision = await evaluateAutonomousPrivateChat({
        firstAiAccountId,
        secondAiAccountId,
      })
      if (currentVersion !== requestVersion.current) return

      setData(decision)
      setStatus('success')
    } catch (error) {
      if (currentVersion !== requestVersion.current) return

      setData(undefined)
      setStatus('error')
      setErrorMessage(
        error instanceof Error ? error.message : '自主私信判断失败。',
      )
    }
  }, [firstAiAccountId, secondAiAccountId])

  return { data, status, errorMessage, evaluate }
}
