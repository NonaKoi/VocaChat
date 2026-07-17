import { useCallback, useEffect, useState } from 'react'
import {
  getAutonomousInteractionSettings,
  updateAutonomousInteractionSettings,
} from '@/api/settings'
import type {
  AutonomousInteractionSettingsResponse,
  UpdateAutonomousInteractionSettingsRequest,
} from '@/api/types'
import type { RemoteStatus } from '@/types/remoteStatus'

export function useAutonomousInteractionSettings() {
  const [data, setData] = useState<AutonomousInteractionSettingsResponse>()
  const [status, setStatus] = useState<RemoteStatus>('loading')
  const [errorMessage, setErrorMessage] = useState<string>()
  const [saveErrorMessage, setSaveErrorMessage] = useState<string>()
  const [isSaving, setIsSaving] = useState(false)

  const reload = useCallback(async () => {
    setStatus('loading')
    setErrorMessage(undefined)

    try {
      setData(await getAutonomousInteractionSettings())
      setStatus('success')
    } catch (error) {
      setStatus('error')
      setErrorMessage(
        error instanceof Error ? error.message : '设置加载失败。',
      )
    }
  }, [])

  useEffect(() => {
    void reload()
  }, [reload])

  async function save(
    request: UpdateAutonomousInteractionSettingsRequest,
  ): Promise<AutonomousInteractionSettingsResponse | undefined> {
    setIsSaving(true)
    setSaveErrorMessage(undefined)

    try {
      const savedSettings = await updateAutonomousInteractionSettings(request)
      setData(savedSettings)
      return savedSettings
    } catch (error) {
      setSaveErrorMessage(
        error instanceof Error ? error.message : '设置保存失败。',
      )
      return undefined
    } finally {
      setIsSaving(false)
    }
  }

  return {
    data,
    status,
    errorMessage,
    saveErrorMessage,
    isSaving,
    reload,
    save,
  }
}
