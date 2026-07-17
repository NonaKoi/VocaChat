import { getJson } from './http'
import type { AiAccountResponse } from './types'

/** 返回当前本地用户已经创建的全部 AI 账号。 */
export function getAiAccounts(): Promise<AiAccountResponse[]> {
  return getJson<AiAccountResponse[]>('/api/ai-accounts')
}
