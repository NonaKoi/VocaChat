import { postJson } from '@/api/http'
import type {
  AutonomousPrivateChatDecisionResponse,
  EvaluateAutonomousPrivateChatRequest,
} from '@/api/types'

export function evaluateAutonomousPrivateChat(
  request: EvaluateAutonomousPrivateChatRequest,
): Promise<AutonomousPrivateChatDecisionResponse> {
  return postJson(
    '/api/autonomous-interactions/private-chat/evaluate',
    request,
  )
}
