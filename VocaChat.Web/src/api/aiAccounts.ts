import { getJson, postJson, putFormData, putJson } from './http'
import type {
  AiAccountResponse,
  CreateAiAccountRequest,
  UpdateAiAccountRequest,
} from './types'

/** 返回当前本地用户已经创建的全部 AI 账号。 */
export function getAiAccounts(): Promise<AiAccountResponse[]> {
  return getJson<AiAccountResponse[]>('/api/ai-accounts')
}

/** 创建一个由当前本地用户管理的长期 AI 账号。 */
export function createAiAccount(
  request: CreateAiAccountRequest,
): Promise<AiAccountResponse> {
  return postJson<AiAccountResponse, CreateAiAccountRequest>(
    '/api/ai-accounts',
    request,
  )
}

/** 更新一个已经存在的长期 AI 账号档案。 */
export function updateAiAccount(
  aiAccountId: string,
  request: UpdateAiAccountRequest,
): Promise<AiAccountResponse> {
  return putJson<AiAccountResponse, UpdateAiAccountRequest>(
    `/api/ai-accounts/${aiAccountId}`,
    request,
  )
}

/** 替换指定好友的本地头像。 */
export function uploadAiAccountAvatar(
  aiAccountId: string,
  file: File,
): Promise<AiAccountResponse> {
  return uploadAiAccountMedia(aiAccountId, 'avatar', file)
}

/** 替换指定好友资料页的本地封面。 */
export function uploadAiAccountCover(
  aiAccountId: string,
  file: File,
): Promise<AiAccountResponse> {
  return uploadAiAccountMedia(aiAccountId, 'cover', file)
}

function uploadAiAccountMedia(
  aiAccountId: string,
  mediaName: 'avatar' | 'cover',
  file: File,
): Promise<AiAccountResponse> {
  const formData = new FormData()
  formData.append('file', file)

  return putFormData<AiAccountResponse>(
    `/api/ai-accounts/${aiAccountId}/${mediaName}`,
    formData,
  )
}
