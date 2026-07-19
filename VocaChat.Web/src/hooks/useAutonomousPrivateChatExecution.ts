import { useCallback, useEffect, useRef, useState } from 'react'
import { runAutonomousPrivateChat } from '@/api/autonomousInteractions'
import type { AutonomousPrivateChatExecutionResponse } from '@/api/types'
import type { RemoteStatus } from '@/types/remoteStatus'

/** 管理用户明确触发的一次自主私信执行，不负责后台调度。 */
export function useAutonomousPrivateChatExecution(
  firstAiAccountId?: string,
  secondAiAccountId?: string,
) {
  const [data, setData] = useState<AutonomousPrivateChatExecutionResponse>()
  const [status, setStatus] = useState<RemoteStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>()
  const requestVersion = useRef(0)

  useEffect(() => {
    requestVersion.current += 1
    setData(undefined)
    setStatus('idle')
    setErrorMessage(undefined)
  }, [firstAiAccountId, secondAiAccountId])

  const execute = useCallback(async (topic?: string) => {
    if (!firstAiAccountId || !secondAiAccountId) return undefined

    const currentVersion = ++requestVersion.current
    setStatus('loading')
    setErrorMessage(undefined)

    try {
      const result = await runAutonomousPrivateChat({
        firstAiAccountId,
        secondAiAccountId,
        topic: topic?.trim() || undefined,
      })
      if (currentVersion !== requestVersion.current) return undefined

      setData(result)
      setStatus('success')
      return result
    } catch (error) {
      if (currentVersion !== requestVersion.current) return undefined

      setData(undefined)
      setStatus('error')
      setErrorMessage(
        error instanceof Error ? error.message : '自主私信执行失败。',
      )
      return undefined
    }
  }, [firstAiAccountId, secondAiAccountId])

  return { data, status, errorMessage, execute }
}
