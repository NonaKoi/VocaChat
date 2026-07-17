import { useCallback, useEffect, useRef, useState } from 'react'
import {
  getAiRelationship,
  updateAiRelationship,
} from '@/api/relationships'
import type {
  AiRelationshipResponse,
  UpdateAiRelationshipRequest,
} from '@/api/types'
import type { RemoteStatus } from '@/types/remoteStatus'

export function useAiRelationship(
  fromAiAccountId?: string,
  toAiAccountId?: string,
) {
  const [data, setData] = useState<AiRelationshipResponse>()
  const [status, setStatus] = useState<RemoteStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>()
  const [saveErrorMessage, setSaveErrorMessage] = useState<string>()
  const [isSaving, setIsSaving] = useState(false)
  const requestVersion = useRef(0)
  const activePair = useRef({ fromAiAccountId, toAiAccountId })
  activePair.current = { fromAiAccountId, toAiAccountId }

  const reload = useCallback(async () => {
    const currentVersion = ++requestVersion.current
    setIsSaving(false)
    setSaveErrorMessage(undefined)

    if (!fromAiAccountId || !toAiAccountId) {
      setData(undefined)
      setErrorMessage(undefined)
      setStatus('idle')
      return
    }

    setStatus('loading')
    setErrorMessage(undefined)

    try {
      const relationship = await getAiRelationship(
        fromAiAccountId,
        toAiAccountId,
      )
      if (currentVersion !== requestVersion.current) return

      setData(relationship)
      setStatus('success')
    } catch (error) {
      if (currentVersion !== requestVersion.current) return

      setData(undefined)
      setStatus('error')
      setErrorMessage(
        error instanceof Error ? error.message : '好友关系加载失败。',
      )
    }
  }, [fromAiAccountId, toAiAccountId])

  useEffect(() => {
    void reload()
  }, [reload])

  async function save(
    request: UpdateAiRelationshipRequest,
  ): Promise<AiRelationshipResponse | undefined> {
    if (!fromAiAccountId || !toAiAccountId) return undefined

    const savingPair = { fromAiAccountId, toAiAccountId }
    setIsSaving(true)
    setSaveErrorMessage(undefined)

    try {
      const relationship = await updateAiRelationship(
        savingPair.fromAiAccountId,
        savingPair.toAiAccountId,
        request,
      )
      if (isSamePair(savingPair, activePair.current)) setData(relationship)
      return relationship
    } catch (error) {
      if (isSamePair(savingPair, activePair.current)) {
        setSaveErrorMessage(
          error instanceof Error ? error.message : '好友关系保存失败。',
        )
      }
      return undefined
    } finally {
      if (isSamePair(savingPair, activePair.current)) setIsSaving(false)
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

function isSamePair(
  first: { fromAiAccountId?: string; toAiAccountId?: string },
  second: { fromAiAccountId?: string; toAiAccountId?: string },
) {
  return first.fromAiAccountId === second.fromAiAccountId
    && first.toAiAccountId === second.toAiAccountId
}
