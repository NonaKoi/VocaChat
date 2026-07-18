import { ApiError, postJson } from '@/api/http'
import type {
  AutonomousPrivateChatExecutionResponse,
  AutonomousPrivateChatDecisionResponse,
  EvaluateAutonomousPrivateChatRequest,
  RunAutonomousPrivateChatRequest,
} from '@/api/types'

export function evaluateAutonomousPrivateChat(
  request: EvaluateAutonomousPrivateChatRequest,
): Promise<AutonomousPrivateChatDecisionResponse> {
  return postJson(
    '/api/autonomous-interactions/private-chat/evaluate',
    request,
  )
}

export async function runAutonomousPrivateChat(
  request: RunAutonomousPrivateChatRequest,
): Promise<AutonomousPrivateChatExecutionResponse> {
  try {
    return await postJson(
      '/api/autonomous-interactions/private-chat/run',
      request,
    )
  } catch (error) {
    if (error instanceof ApiError && isExecutionResponse(error.responseBody)) {
      return error.responseBody
    }

    throw error
  }
}

function isExecutionResponse(
  value: unknown,
): value is AutonomousPrivateChatExecutionResponse {
  if (typeof value !== 'object' || value === null) return false

  const candidate = value as Record<string, unknown>
  return typeof candidate.status === 'string'
    && typeof candidate.decision === 'object'
    && candidate.decision !== null
}
