import { useCallback, useEffect, useState } from 'react'
import {
  createCharacterWorld,
  getCharacterWorlds,
  updateCharacterWorld,
} from '@/api/characterWorlds'
import type {
  CharacterWorldResponse,
  CreateCharacterWorldRequest,
  UpdateCharacterWorldRequest,
} from '@/api/types'
import type { RemoteStatus } from '@/types/remoteStatus'

export interface CharacterWorldsState {
  data: CharacterWorldResponse[]
  status: RemoteStatus
  errorMessage?: string
  mutationErrorMessage?: string
  isCreating: boolean
  updatingWorldId?: string
  reload: () => void
  create: (
    request: CreateCharacterWorldRequest,
  ) => Promise<CharacterWorldResponse | undefined>
  update: (
    worldId: string,
    request: UpdateCharacterWorldRequest,
  ) => Promise<CharacterWorldResponse | undefined>
  clearMutationError: () => void
}

/** 管理设置页所需的角色世界读取、创建和更新状态。 */
export function useCharacterWorlds(): CharacterWorldsState {
  const [data, setData] = useState<CharacterWorldResponse[]>([])
  const [status, setStatus] = useState<RemoteStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>()
  const [mutationErrorMessage, setMutationErrorMessage] = useState<string>()
  const [isCreating, setIsCreating] = useState(false)
  const [updatingWorldId, setUpdatingWorldId] = useState<string>()

  const reload = useCallback(() => {
    setStatus('loading')
    setErrorMessage(undefined)

    void getCharacterWorlds()
      .then((worlds) => {
        setData(worlds)
        setStatus('success')
      })
      .catch((error: unknown) => {
        setStatus('error')
        setErrorMessage(
          error instanceof Error
            ? error.message
            : '角色世界加载失败，请重试。',
        )
      })
  }, [])

  useEffect(() => {
    reload()
  }, [reload])

  const create = useCallback(
    async (
      request: CreateCharacterWorldRequest,
    ): Promise<CharacterWorldResponse | undefined> => {
      if (isCreating || updatingWorldId) return undefined

      setIsCreating(true)
      setMutationErrorMessage(undefined)

      try {
        const world = await createCharacterWorld(request)
        setData((current) => [...current, world])
        return world
      } catch (error: unknown) {
        setMutationErrorMessage(
          error instanceof Error
            ? error.message
            : '角色世界创建失败，请重试。',
        )
        return undefined
      } finally {
        setIsCreating(false)
      }
    },
    [isCreating, updatingWorldId],
  )

  const update = useCallback(
    async (
      worldId: string,
      request: UpdateCharacterWorldRequest,
    ): Promise<CharacterWorldResponse | undefined> => {
      if (isCreating || updatingWorldId) return undefined

      setUpdatingWorldId(worldId)
      setMutationErrorMessage(undefined)

      try {
        const world = await updateCharacterWorld(worldId, request)
        setData((current) => current.map((item) => (
          item.id === world.id ? world : item
        )))
        return world
      } catch (error: unknown) {
        setMutationErrorMessage(
          error instanceof Error
            ? error.message
            : '角色世界保存失败，请重试。',
        )
        return undefined
      } finally {
        setUpdatingWorldId(undefined)
      }
    },
    [isCreating, updatingWorldId],
  )

  return {
    data,
    status,
    errorMessage,
    mutationErrorMessage,
    isCreating,
    updatingWorldId,
    reload,
    create,
    update,
    clearMutationError: () => setMutationErrorMessage(undefined),
  }
}
