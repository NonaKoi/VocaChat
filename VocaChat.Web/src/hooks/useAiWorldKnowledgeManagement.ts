import { useCallback, useEffect, useState } from 'react'
import {
  archiveAiWorldKnowledge,
  getAiWorldAwareness,
  getAiWorldKnowledge,
  getAiWorldKnowledgeEvidence,
  updateAiWorldKnowledge,
  updateAiWorldKnowledgeLock,
  updateParallelWorldAwareness,
  updateSubjectWorldAwareness,
} from '@/api/aiWorldKnowledge'
import type {
  AiParallelWorldAwarenessState,
  AiWorldAwarenessOverviewResponse,
  AiWorldAwarenessState,
  AiWorldKnowledgeResponse,
  AiWorldKnowledgeStatus,
  UpdateAiWorldKnowledgeRequest,
} from '@/api/types'
import type { RemoteStatus } from '@/types/remoteStatus'

export function useAiWorldKnowledgeManagement(
  aiAccountId: string,
  subjectAiAccountId: string | undefined,
  statusFilter: AiWorldKnowledgeStatus,
) {
  const [overview, setOverview] = useState<AiWorldAwarenessOverviewResponse>()
  const [knowledge, setKnowledge] = useState<AiWorldKnowledgeResponse[]>([])
  const [overviewStatus, setOverviewStatus] = useState<RemoteStatus>('idle')
  const [knowledgeStatus, setKnowledgeStatus] = useState<RemoteStatus>('idle')
  const [overviewError, setOverviewError] = useState<string>()
  const [knowledgeError, setKnowledgeError] = useState<string>()
  const [operationError, setOperationError] = useState<string>()
  const [operation, setOperation] = useState<string>()

  const reloadOverview = useCallback(async () => {
    if (!aiAccountId) return
    setOverviewStatus('loading')
    setOverviewError(undefined)
    try {
      setOverview(await getAiWorldAwareness(aiAccountId))
      setOverviewStatus('success')
    } catch (error: unknown) {
      setOverviewStatus('error')
      setOverviewError(toMessage(error, '世界认知加载失败，请重试。'))
    }
  }, [aiAccountId])

  const reloadKnowledge = useCallback(async () => {
    if (!aiAccountId) return
    setKnowledgeStatus('loading')
    setKnowledgeError(undefined)
    try {
      setKnowledge(await getAiWorldKnowledge(
        aiAccountId,
        subjectAiAccountId,
        statusFilter,
      ))
      setKnowledgeStatus('success')
    } catch (error: unknown) {
      setKnowledge([])
      setKnowledgeStatus('error')
      setKnowledgeError(toMessage(error, '世界知识加载失败，请重试。'))
    }
  }, [aiAccountId, statusFilter, subjectAiAccountId])

  useEffect(() => {
    void reloadOverview()
  }, [reloadOverview])

  useEffect(() => {
    void reloadKnowledge()
  }, [reloadKnowledge])

  const runOperation = useCallback(async <T,>(
    key: string,
    action: () => Promise<T>,
    reload: 'overview' | 'knowledge' | 'both',
  ): Promise<T | undefined> => {
    if (operation) return undefined
    setOperation(key)
    setOperationError(undefined)
    try {
      const result = await action()
      if (reload !== 'knowledge') await reloadOverview()
      if (reload !== 'overview') await reloadKnowledge()
      return result
    } catch (error: unknown) {
      setOperationError(toMessage(error, '世界认知修改失败，请重试。'))
      return undefined
    } finally {
      setOperation(undefined)
    }
  }, [operation, reloadKnowledge, reloadOverview])

  const loadEvidence = useCallback(
    (knowledgeId: string) =>
      getAiWorldKnowledgeEvidence(aiAccountId, knowledgeId),
    [aiAccountId],
  )

  return {
    overview,
    knowledge,
    overviewStatus,
    knowledgeStatus,
    overviewError,
    knowledgeError,
    operationError,
    operation,
    reloadOverview,
    reloadKnowledge,
    clearOperationError: () => setOperationError(undefined),
    updateParallel: (
      state: AiParallelWorldAwarenessState,
      isUserLocked: boolean,
    ) => runOperation(
      'parallel',
      () => updateParallelWorldAwareness(aiAccountId, state, isUserLocked),
      'overview',
    ),
    updateSubject: (
      subjectId: string,
      state: AiWorldAwarenessState,
      isUserLocked: boolean,
    ) => runOperation(
      `subject:${subjectId}`,
      () => updateSubjectWorldAwareness(
        aiAccountId,
        subjectId,
        state,
        isUserLocked,
      ),
      'overview',
    ),
    updateKnowledge: (
      knowledgeId: string,
      request: UpdateAiWorldKnowledgeRequest,
    ) => runOperation(
      `knowledge:${knowledgeId}`,
      () => updateAiWorldKnowledge(aiAccountId, knowledgeId, request),
      'both',
    ),
    updateLock: (knowledgeId: string, isUserLocked: boolean) =>
      runOperation(
        `lock:${knowledgeId}`,
        () => updateAiWorldKnowledgeLock(
          aiAccountId,
          knowledgeId,
          isUserLocked,
        ),
        'knowledge',
      ),
    archive: (knowledgeId: string) => runOperation(
      `archive:${knowledgeId}`,
      () => archiveAiWorldKnowledge(aiAccountId, knowledgeId),
      'both',
    ),
    loadEvidence,
  }
}

function toMessage(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback
}
