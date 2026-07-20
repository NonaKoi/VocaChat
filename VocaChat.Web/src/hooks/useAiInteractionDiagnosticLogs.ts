import { useCallback, useEffect, useState } from 'react'
import { getAiInteractionDiagnosticLogs } from '@/api/settings'
import type { AiInteractionDiagnosticLogResponse } from '@/api/types'
import type { RemoteStatus } from '@/types/remoteStatus'

export function useAiInteractionDiagnosticLogs() {
  const [data, setData] = useState<AiInteractionDiagnosticLogResponse[]>([])
  const [status, setStatus] = useState<RemoteStatus>('loading')
  const [errorMessage, setErrorMessage] = useState<string>()

  const reload = useCallback(async () => {
    setStatus('loading')
    setErrorMessage(undefined)
    try {
      setData(await getAiInteractionDiagnosticLogs())
      setStatus('success')
    } catch (error) {
      setStatus('error')
      setErrorMessage(
        error instanceof Error ? error.message : '互动日志加载失败。',
      )
    }
  }, [])

  useEffect(() => {
    void reload()
  }, [reload])

  return { data, status, errorMessage, reload }
}

