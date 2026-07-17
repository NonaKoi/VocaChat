import { useCallback, useEffect, useRef, useState } from 'react'
import {
  getAiAccountAutonomySettings,
  updateAiAccountAutonomySettings,
} from '@/api/settings'
import type {
  AiAccountAutonomySettingsResponse,
  UpdateAiAccountAutonomySettingsRequest,
} from '@/api/types'
import type { RemoteStatus } from '@/types/remoteStatus'

export function useAiAccountAutonomySettings(aiAccountId?: string) {
  const [data, setData] = useState<AiAccountAutonomySettingsResponse>()
  const [status, setStatus] = useState<RemoteStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>()
  const [saveErrorMessage, setSaveErrorMessage] = useState<string>()
  const [isSaving, setIsSaving] = useState(false)
  const requestVersion = useRef(0)
  const activeAccountId = useRef(aiAccountId)
  activeAccountId.current = aiAccountId

  const reload = useCallback(async () => {
    const currentVersion = ++requestVersion.current
    setIsSaving(false)
    setSaveErrorMessage(undefined)

    if (!aiAccountId) {
      setData(undefined)
      setErrorMessage(undefined)
      setStatus('idle')
      return
    }

    setStatus('loading')
    setErrorMessage(undefined)

    try {
      const loadedSettings = await getAiAccountAutonomySettings(aiAccountId)
      if (currentVersion !== requestVersion.current) return

      setData(loadedSettings)
      setStatus('success')
    } catch (error) {
      if (currentVersion !== requestVersion.current) return

      setData(undefined)
      setStatus('error')
      setErrorMessage(
        error instanceof Error ? error.message : '好友设置加载失败。',
      )
    }
  }, [aiAccountId])

  useEffect(() => {
    void reload()
  }, [reload])

  async function save(
    request: UpdateAiAccountAutonomySettingsRequest,
  ): Promise<AiAccountAutonomySettingsResponse | undefined> {
    if (!aiAccountId) return undefined

    const savingAccountId = aiAccountId
    setIsSaving(true)
    setSaveErrorMessage(undefined)

    try {
      const savedSettings = await updateAiAccountAutonomySettings(
        savingAccountId,
        request,
      )

      if (savingAccountId === activeAccountId.current) setData(savedSettings)
      return savedSettings
    } catch (error) {
      if (savingAccountId === activeAccountId.current) {
        setSaveErrorMessage(
          error instanceof Error ? error.message : '好友设置保存失败。',
        )
      }
      return undefined
    } finally {
      if (savingAccountId === activeAccountId.current) setIsSaving(false)
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
