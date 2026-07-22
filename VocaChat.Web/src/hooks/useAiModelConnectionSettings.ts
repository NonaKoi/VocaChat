import { useCallback, useEffect, useState } from 'react'
import {
  getAiModelConnectionSettings,
  updateAiModelConnectionSettings,
} from '@/api/settings'
import type {
  AiModelConnectionSettingsResponse,
  UpdateAiModelConnectionSettingsRequest,
} from '@/api/types'
import type { RemoteStatus } from '@/types/remoteStatus'

export function useAiModelConnectionSettings() {
  const [data, setData] = useState<AiModelConnectionSettingsResponse>()
  const [status, setStatus] = useState<RemoteStatus>('loading')
  const [errorMessage, setErrorMessage] = useState<string>()
  const [saveErrorMessage, setSaveErrorMessage] = useState<string>()
  const [isSaving, setIsSaving] = useState(false)

  const reload = useCallback(async () => {
    setStatus('loading')
    setErrorMessage(undefined)

    try {
      setData(await getAiModelConnectionSettings())
      setStatus('success')
    } catch (error) {
      setStatus('error')
      setErrorMessage(
        error instanceof Error ? error.message : 'AI 接口设置加载失败。',
      )
    }
  }, [])

  useEffect(() => {
    void reload()
  }, [reload])

  async function save(
    request: UpdateAiModelConnectionSettingsRequest,
  ): Promise<AiModelConnectionSettingsResponse | undefined> {
    setIsSaving(true)
    setSaveErrorMessage(undefined)

    try {
      const saved = await updateAiModelConnectionSettings(request)
      setData(saved)
      return saved
    } catch (error) {
      setSaveErrorMessage(
        error instanceof Error ? error.message : 'AI 接口设置保存失败。',
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
