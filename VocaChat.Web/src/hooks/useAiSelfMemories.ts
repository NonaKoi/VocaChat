import { useCallback, useEffect, useState } from 'react'
import {
  createAiSelfMemory,
  getAiSelfMemories,
  updateAiSelfMemory,
  updateAiSelfMemoryStatus,
} from '@/api/aiSelfMemories'
import type {
  AiSelfMemoryResponse,
  AiSelfMemoryStatus,
  SaveAiSelfMemoryRequest,
} from '@/api/types'
import type { RemoteStatus } from '@/types/remoteStatus'

interface AiSelfMemoriesState {
  data: AiSelfMemoryResponse[]
  status: RemoteStatus
  errorMessage?: string
  operationErrorMessage?: string
  operation?: { kind: 'create' | 'update' | 'status'; memoryId?: string }
  reload: () => Promise<void>
  create: (
    request: SaveAiSelfMemoryRequest,
  ) => Promise<AiSelfMemoryResponse | undefined>
  update: (
    memoryId: string,
    request: SaveAiSelfMemoryRequest,
  ) => Promise<AiSelfMemoryResponse | undefined>
  changeStatus: (
    memoryId: string,
    status: 'Active' | 'Archived',
  ) => Promise<AiSelfMemoryResponse | undefined>
  clearOperationError: () => void
}

/** 管理一个账号的个人记忆远程状态，页面不直接调用 fetch。 */
export function useAiSelfMemories(
  aiAccountId: string,
  statusFilter?: AiSelfMemoryStatus,
): AiSelfMemoriesState {
  const [data, setData] = useState<AiSelfMemoryResponse[]>([])
  const [status, setStatus] = useState<RemoteStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>()
  const [operationErrorMessage, setOperationErrorMessage] = useState<string>()
  const [operation, setOperation] = useState<AiSelfMemoriesState['operation']>()

  const reload = useCallback(async () => {
    if (!aiAccountId) {
      setData([])
      setStatus('idle')
      return
    }

    setStatus('loading')
    setErrorMessage(undefined)

    try {
      const memories = await getAiSelfMemories(aiAccountId, statusFilter)
      setData(memories)
      setStatus('success')
    } catch (error: unknown) {
      setData([])
      setStatus('error')
      setErrorMessage(
        error instanceof Error ? error.message : 'AI 记忆加载失败，请重试。',
      )
    }
  }, [aiAccountId, statusFilter])

  useEffect(() => {
    void reload()
  }, [reload])

  const create = useCallback(async (request: SaveAiSelfMemoryRequest) => {
    if (!aiAccountId || operation) return undefined
    setOperation({ kind: 'create' })
    setOperationErrorMessage(undefined)

    try {
      const memory = await createAiSelfMemory(aiAccountId, request)
      await reload()
      return memory
    } catch (error: unknown) {
      setOperationErrorMessage(
        error instanceof Error ? error.message : 'AI 记忆保存失败，请重试。',
      )
      return undefined
    } finally {
      setOperation(undefined)
    }
  }, [aiAccountId, operation, reload])

  const update = useCallback(async (
    memoryId: string,
    request: SaveAiSelfMemoryRequest,
  ) => {
    if (!aiAccountId || operation) return undefined
    setOperation({ kind: 'update', memoryId })
    setOperationErrorMessage(undefined)

    try {
      const memory = await updateAiSelfMemory(aiAccountId, memoryId, request)
      await reload()
      return memory
    } catch (error: unknown) {
      setOperationErrorMessage(
        error instanceof Error ? error.message : 'AI 记忆更新失败，请重试。',
      )
      return undefined
    } finally {
      setOperation(undefined)
    }
  }, [aiAccountId, operation, reload])

  const changeStatus = useCallback(async (
    memoryId: string,
    nextStatus: 'Active' | 'Archived',
  ) => {
    if (!aiAccountId || operation) return undefined
    setOperation({ kind: 'status', memoryId })
    setOperationErrorMessage(undefined)

    try {
      const memory = await updateAiSelfMemoryStatus(
        aiAccountId,
        memoryId,
        nextStatus,
      )
      await reload()
      return memory
    } catch (error: unknown) {
      setOperationErrorMessage(
        error instanceof Error ? error.message : 'AI 记忆状态更新失败，请重试。',
      )
      return undefined
    } finally {
      setOperation(undefined)
    }
  }, [aiAccountId, operation, reload])

  return {
    data,
    status,
    errorMessage,
    operationErrorMessage,
    operation,
    reload,
    create,
    update,
    changeStatus,
    clearOperationError: () => setOperationErrorMessage(undefined),
  }
}
