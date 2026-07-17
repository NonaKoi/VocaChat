import { getJson, putJson } from '@/api/http'
import type {
  AiRelationshipResponse,
  UpdateAiRelationshipRequest,
} from '@/api/types'

export function getAiRelationship(
  fromAiAccountId: string,
  toAiAccountId: string,
): Promise<AiRelationshipResponse> {
  return getJson(
    `/api/ai-accounts/${fromAiAccountId}/relationships/${toAiAccountId}`,
  )
}

export function updateAiRelationship(
  fromAiAccountId: string,
  toAiAccountId: string,
  request: UpdateAiRelationshipRequest,
): Promise<AiRelationshipResponse> {
  return putJson(
    `/api/ai-accounts/${fromAiAccountId}/relationships/${toAiAccountId}`,
    request,
  )
}
