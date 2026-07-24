import { getJson, postJson, putJson } from './http'
import type {
  CharacterWorldResponse,
  CreateCharacterWorldRequest,
  UpdateCharacterWorldRequest,
} from './types'

/** 返回当前本地数据中可供好友共享的全部角色世界。 */
export function getCharacterWorlds(): Promise<CharacterWorldResponse[]> {
  return getJson<CharacterWorldResponse[]>('/api/character-worlds')
}

/** 创建一个新的角色世界。 */
export function createCharacterWorld(
  request: CreateCharacterWorldRequest,
): Promise<CharacterWorldResponse> {
  return postJson<CharacterWorldResponse, CreateCharacterWorldRequest>(
    '/api/character-worlds',
    request,
  )
}

/** 更新一个已经存在的共享角色世界。 */
export function updateCharacterWorld(
  characterWorldId: string,
  request: UpdateCharacterWorldRequest,
): Promise<CharacterWorldResponse> {
  return putJson<CharacterWorldResponse, UpdateCharacterWorldRequest>(
    `/api/character-worlds/${characterWorldId}`,
    request,
  )
}
