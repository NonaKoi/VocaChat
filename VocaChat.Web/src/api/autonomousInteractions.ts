import { ApiError, postJson } from '@/api/http'
import type {
  AutonomousPrivateChatExecutionResponse,
  AutonomousPrivateChatDecisionResponse,
  EvaluateAutonomousPrivateChatRequest,
  RunAutonomousPrivateChatRequest,
  AutonomousGroupChatDecisionResponse,
  AutonomousGroupChatExecutionResponse,
  EvaluateAutonomousGroupChatRequest,
  RunAutonomousGroupChatRequest,
} from '@/api/types'

export function evaluateAutonomousPrivateChat(
  request: EvaluateAutonomousPrivateChatRequest,
): Promise<AutonomousPrivateChatDecisionResponse> {
  return postJson(
    '/api/autonomous-interactions/private-chat/evaluate',
    request,
  )
}

export function evaluateAutonomousGroupChat(
  request: EvaluateAutonomousGroupChatRequest,
): Promise<AutonomousGroupChatDecisionResponse> {
  return postJson(
    '/api/autonomous-interactions/group-chat/evaluate',
    request,
  )
}

export async function runAutonomousGroupChat(
  request: RunAutonomousGroupChatRequest,
): Promise<AutonomousGroupChatExecutionResponse> {
  try {
    return await postJson(
      '/api/autonomous-interactions/group-chat/run',
      request,
    )
  } catch (error) {
    if (error instanceof ApiError && isGroupExecutionResponse(error.responseBody)) {
      return error.responseBody
    }

    throw error
  }
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

function isGroupExecutionResponse(
  value: unknown,
): value is AutonomousGroupChatExecutionResponse {
  if (typeof value !== 'object' || value === null) return false

  const candidate = value as Record<string, unknown>
  return typeof candidate.status === 'string'
    && typeof candidate.decision === 'object'
    && candidate.decision !== null
}
