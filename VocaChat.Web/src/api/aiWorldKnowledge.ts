import { getJson, putJson } from './http'
import type {
  AiParallelWorldAwarenessState,
  AiWorldAwarenessOverviewResponse,
  AiWorldAwarenessState,
  AiWorldKnowledgeEvidenceResponse,
  AiWorldKnowledgeResponse,
  AiWorldKnowledgeStatus,
  ParallelWorldAwarenessResponse,
  UpdateAiWorldAwarenessRequest,
  UpdateAiWorldKnowledgeRequest,
  WorldAwarenessSubjectResponse,
} from './types'

export function getAiWorldAwareness(
  aiAccountId: string,
): Promise<AiWorldAwarenessOverviewResponse> {
  return getJson(`/api/ai-accounts/${aiAccountId}/world-awareness`)
}

export function updateParallelWorldAwareness(
  aiAccountId: string,
  state: AiParallelWorldAwarenessState,
  isUserLocked: boolean,
): Promise<ParallelWorldAwarenessResponse> {
  const request: UpdateAiWorldAwarenessRequest = { state, isUserLocked }
  return putJson(
    `/api/ai-accounts/${aiAccountId}/world-awareness/parallel`,
    request,
  )
}

export function updateSubjectWorldAwareness(
  aiAccountId: string,
  subjectAiAccountId: string,
  state: AiWorldAwarenessState,
  isUserLocked: boolean,
): Promise<WorldAwarenessSubjectResponse> {
  const request: UpdateAiWorldAwarenessRequest = { state, isUserLocked }
  return putJson(
    `/api/ai-accounts/${aiAccountId}/world-awareness/subjects/${subjectAiAccountId}`,
    request,
  )
}

export function getAiWorldKnowledge(
  aiAccountId: string,
  subjectAiAccountId: string | undefined,
  status: AiWorldKnowledgeStatus,
): Promise<AiWorldKnowledgeResponse[]> {
  const query = new URLSearchParams({ status })
  if (subjectAiAccountId) query.set('subjectAiAccountId', subjectAiAccountId)
  return getJson(
    `/api/ai-accounts/${aiAccountId}/world-knowledge?${query.toString()}`,
  )
}

export function getAiWorldKnowledgeEvidence(
  aiAccountId: string,
  knowledgeId: string,
): Promise<AiWorldKnowledgeEvidenceResponse[]> {
  return getJson(
    `/api/ai-accounts/${aiAccountId}/world-knowledge/${knowledgeId}/evidence`,
  )
}

export function updateAiWorldKnowledge(
  aiAccountId: string,
  knowledgeId: string,
  request: UpdateAiWorldKnowledgeRequest,
): Promise<AiWorldKnowledgeResponse> {
  return putJson(
    `/api/ai-accounts/${aiAccountId}/world-knowledge/${knowledgeId}`,
    request,
  )
}

export function updateAiWorldKnowledgeLock(
  aiAccountId: string,
  knowledgeId: string,
  isUserLocked: boolean,
): Promise<AiWorldKnowledgeResponse> {
  return putJson(
    `/api/ai-accounts/${aiAccountId}/world-knowledge/${knowledgeId}/lock`,
    { isUserLocked },
  )
}

export function archiveAiWorldKnowledge(
  aiAccountId: string,
  knowledgeId: string,
): Promise<AiWorldKnowledgeResponse> {
  return putJson<
    AiWorldKnowledgeResponse,
    Record<string, never>
  >(
    `/api/ai-accounts/${aiAccountId}/world-knowledge/${knowledgeId}/archive`,
    {},
  )
}
