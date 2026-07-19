import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  evaluateAutonomousGroupChat,
  runAutonomousGroupChat,
} from '@/api/autonomousInteractions'
import type {
  AutonomousGroupChatDecisionResponse,
  AutonomousGroupChatExecutionResponse,
} from '@/api/types'
import type { RemoteStatus } from '@/types/remoteStatus'

/** 管理用户明确触发的自主好友群聊预览与执行，不负责后台调度。 */
export function useAutonomousGroupChat(participantAiAccountIds: string[]) {
  const participantKey = useMemo(
    () => [...participantAiAccountIds].sort().join(','),
    [participantAiAccountIds],
  )
  const [decision, setDecision] = useState<AutonomousGroupChatDecisionResponse>()
  const [execution, setExecution] = useState<AutonomousGroupChatExecutionResponse>()
  const [previewStatus, setPreviewStatus] = useState<RemoteStatus>('idle')
  const [executionStatus, setExecutionStatus] = useState<RemoteStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>()
  const requestVersion = useRef(0)

  useEffect(() => {
    requestVersion.current += 1
    setDecision(undefined)
    setExecution(undefined)
    setPreviewStatus('idle')
    setExecutionStatus('idle')
    setErrorMessage(undefined)
  }, [participantKey])

  const evaluate = useCallback(async () => {
    const currentVersion = ++requestVersion.current
    setPreviewStatus('loading')
    setErrorMessage(undefined)

    try {
      const result = await evaluateAutonomousGroupChat({
        participantAiAccountIds,
      })
      if (currentVersion !== requestVersion.current) return undefined
      setDecision(result)
      setExecution(undefined)
      setPreviewStatus('success')
      return result
    } catch (error) {
      if (currentVersion !== requestVersion.current) return undefined
      setPreviewStatus('error')
      setErrorMessage(
        error instanceof Error ? error.message : '好友群聊判断失败。',
      )
      return undefined
    }
  }, [participantAiAccountIds])

  const execute = useCallback(async (topic?: string) => {
    const currentVersion = ++requestVersion.current
    setExecutionStatus('loading')
    setErrorMessage(undefined)

    try {
      const result = await runAutonomousGroupChat({
        participantAiAccountIds,
        topic: topic?.trim() || undefined,
      })
      if (currentVersion !== requestVersion.current) return undefined
      setDecision(result.decision)
      setExecution(result)
      setExecutionStatus('success')
      return result
    } catch (error) {
      if (currentVersion !== requestVersion.current) return undefined
      setExecutionStatus('error')
      setErrorMessage(
        error instanceof Error ? error.message : '好友群聊执行失败。',
      )
      return undefined
    }
  }, [participantAiAccountIds])

  return {
    decision,
    execution,
    previewStatus,
    executionStatus,
    errorMessage,
    evaluate,
    execute,
  }
}
