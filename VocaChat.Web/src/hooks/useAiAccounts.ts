import { useCallback, useEffect, useState } from 'react'
import {
  createAiAccount,
  getAiAccounts,
  updateAiAccount,
  uploadAiAccountAvatar,
  uploadAiAccountCover,
} from '@/api/aiAccounts'
import type {
  AiAccountResponse,
  CreateAiAccountRequest,
  UpdateAiAccountRequest,
} from '@/api/types'
import type { RemoteStatus } from '@/types/remoteStatus'

interface AiAccountsState {
  data: AiAccountResponse[]
  status: RemoteStatus
  errorMessage?: string
  createErrorMessage?: string
  isCreating: boolean
  updatingAccountId?: string
  updateErrorMessage?: string
  uploadingMedia?: { accountId: string; kind: AiAccountMediaKind }
  mediaUploadErrorMessage?: string
  reload: () => void
  create: (
    request: CreateAiAccountRequest,
  ) => Promise<AiAccountResponse | undefined>
  clearCreateError: () => void
  update: (
    accountId: string,
    request: UpdateAiAccountRequest,
  ) => Promise<AiAccountResponse | undefined>
  clearUpdateError: () => void
  uploadAvatar: (accountId: string, file: File) => Promise<boolean>
  uploadCover: (accountId: string, file: File) => Promise<boolean>
  clearMediaUploadError: () => void
}

type AiAccountMediaKind = 'avatar' | 'cover'

/** 管理 AI 账号列表读取和创建，不把 HTTP 状态散落到页面组件。 */
export function useAiAccounts(): AiAccountsState {
  const [data, setData] = useState<AiAccountResponse[]>([])
  const [status, setStatus] = useState<RemoteStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>()
  const [createErrorMessage, setCreateErrorMessage] = useState<string>()
  const [isCreating, setIsCreating] = useState(false)
  const [updatingAccountId, setUpdatingAccountId] = useState<string>()
  const [updateErrorMessage, setUpdateErrorMessage] = useState<string>()
  const [uploadingMedia, setUploadingMedia] = useState<
    AiAccountsState['uploadingMedia']
  >()
  const [mediaUploadErrorMessage, setMediaUploadErrorMessage] =
    useState<string>()

  const reload = useCallback(() => {
    setStatus('loading')
    setErrorMessage(undefined)

    void getAiAccounts()
      .then((accounts) => {
        setData(accounts)
        setStatus('success')
      })
      .catch((error: unknown) => {
        setStatus('error')
        setErrorMessage(
          error instanceof Error ? error.message : '好友列表加载失败。',
        )
      })
  }, [])

  useEffect(() => {
    reload()
  }, [reload])

  const create = useCallback(
    async (
      request: CreateAiAccountRequest,
    ): Promise<AiAccountResponse | undefined> => {
      if (isCreating) {
        return undefined
      }

      setIsCreating(true)
      setCreateErrorMessage(undefined)

      try {
        const account = await createAiAccount(request)
        setData((current) => [...current, account])
        return account
      } catch (error: unknown) {
        setCreateErrorMessage(
          error instanceof Error ? error.message : '添加好友失败，请重试。',
        )
        return undefined
      } finally {
        setIsCreating(false)
      }
    },
    [isCreating],
  )

  const uploadMedia = useCallback(
    async (
      accountId: string,
      kind: AiAccountMediaKind,
      file: File,
    ): Promise<boolean> => {
      if (uploadingMedia) {
        return false
      }

      setUploadingMedia({ accountId, kind })
      setMediaUploadErrorMessage(undefined)

      try {
        const account =
          kind === 'avatar'
            ? await uploadAiAccountAvatar(accountId, file)
            : await uploadAiAccountCover(accountId, file)

        setData((current) =>
          current.map((item) => (item.id === account.id ? account : item)),
        )
        return true
      } catch (error: unknown) {
        setMediaUploadErrorMessage(
          error instanceof Error ? error.message : '图片上传失败，请重试。',
        )
        return false
      } finally {
        setUploadingMedia(undefined)
      }
    },
    [uploadingMedia],
  )

  const update = useCallback(
    async (
      accountId: string,
      request: UpdateAiAccountRequest,
    ): Promise<AiAccountResponse | undefined> => {
      if (updatingAccountId) {
        return undefined
      }

      setUpdatingAccountId(accountId)
      setUpdateErrorMessage(undefined)

      try {
        const account = await updateAiAccount(accountId, request)
        setData((current) =>
          current.map((item) => (item.id === account.id ? account : item)),
        )
        return account
      } catch (error: unknown) {
        setUpdateErrorMessage(
          error instanceof Error ? error.message : '账号资料保存失败，请重试。',
        )
        return undefined
      } finally {
        setUpdatingAccountId(undefined)
      }
    },
    [updatingAccountId],
  )

  return {
    data,
    status,
    errorMessage,
    createErrorMessage,
    isCreating,
    updatingAccountId,
    updateErrorMessage,
    uploadingMedia,
    mediaUploadErrorMessage,
    reload,
    create,
    clearCreateError: () => setCreateErrorMessage(undefined),
    update,
    clearUpdateError: () => setUpdateErrorMessage(undefined),
    uploadAvatar: (accountId, file) =>
      uploadMedia(accountId, 'avatar', file),
    uploadCover: (accountId, file) =>
      uploadMedia(accountId, 'cover', file),
    clearMediaUploadError: () => setMediaUploadErrorMessage(undefined),
  }
}
