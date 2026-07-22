import { useCallback, useEffect, useRef, useState } from 'react'
import {
  getAiAccountModelConnectionSettings,
  updateAiAccountModelConnectionSettings,
} from '@/api/settings'
import type {
  AiAccountModelConnectionSettingsResponse,
  UpdateAiAccountModelConnectionSettingsRequest,
} from '@/api/types'
import type { RemoteStatus } from '@/types/remoteStatus'

export function useAiAccountModelConnectionSettings(aiAccountId?: string) {
  const [data, setData] = useState<AiAccountModelConnectionSettingsResponse>()
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
      setStatus('idle')
      setErrorMessage(undefined)
      return
    }

    setStatus('loading')
    setErrorMessage(undefined)

    try {
      const loaded = await getAiAccountModelConnectionSettings(aiAccountId)
      if (currentVersion !== requestVersion.current) return
      setData(loaded)
      setStatus('success')
    } catch (error) {
      if (currentVersion !== requestVersion.current) return
      setData(undefined)
      setStatus('error')
      setErrorMessage(
        error instanceof Error ? error.message : '好友 AI 接口设置加载失败。',
      )
    }
  }, [aiAccountId])

  useEffect(() => {
    void reload()
  }, [reload])

  async function save(
    request: UpdateAiAccountModelConnectionSettingsRequest,
  ): Promise<AiAccountModelConnectionSettingsResponse | undefined> {
    if (!aiAccountId) return undefined

    const savingAccountId = aiAccountId
    setIsSaving(true)
    setSaveErrorMessage(undefined)

    try {
      const saved = await updateAiAccountModelConnectionSettings(
        savingAccountId,
        request,
      )
      if (savingAccountId === activeAccountId.current) setData(saved)
      return saved
    } catch (error) {
      if (savingAccountId === activeAccountId.current) {
        setSaveErrorMessage(
          error instanceof Error ? error.message : '好友 AI 接口设置保存失败。',
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
