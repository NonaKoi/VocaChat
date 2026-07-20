import { getJson, postJson, putJson } from './http'
import type {
  AiSelfMemoryResponse,
  AiSelfMemoryStatus,
  SaveAiSelfMemoryRequest,
  UpdateAiSelfMemoryStatusRequest,
} from './types'

/** 查询一个账号的个人记忆；状态筛选由后端负责。 */
export function getAiSelfMemories(
  aiAccountId: string,
  status?: AiSelfMemoryStatus,
): Promise<AiSelfMemoryResponse[]> {
  const query = status ? `?status=${encodeURIComponent(status)}` : ''
  return getJson<AiSelfMemoryResponse[]>(
    `/api/ai-accounts/${aiAccountId}/self-memories${query}`,
  )
}

/** 新增一条由本地用户确认的个人记忆。 */
export function createAiSelfMemory(
  aiAccountId: string,
  request: SaveAiSelfMemoryRequest,
): Promise<AiSelfMemoryResponse> {
  return postJson<AiSelfMemoryResponse, SaveAiSelfMemoryRequest>(
    `/api/ai-accounts/${aiAccountId}/self-memories`,
    request,
  )
}

/** 修改一条属于指定账号的个人记忆。 */
export function updateAiSelfMemory(
  aiAccountId: string,
  memoryId: string,
  request: SaveAiSelfMemoryRequest,
): Promise<AiSelfMemoryResponse> {
  return putJson<AiSelfMemoryResponse, SaveAiSelfMemoryRequest>(
    `/api/ai-accounts/${aiAccountId}/self-memories/${memoryId}`,
    request,
  )
}

/** 归档个人记忆，或恢复为可用状态。 */
export function updateAiSelfMemoryStatus(
  aiAccountId: string,
  memoryId: string,
  status: UpdateAiSelfMemoryStatusRequest['status'],
): Promise<AiSelfMemoryResponse> {
  return putJson<AiSelfMemoryResponse, UpdateAiSelfMemoryStatusRequest>(
    `/api/ai-accounts/${aiAccountId}/self-memories/${memoryId}/status`,
    { status },
  )
}
