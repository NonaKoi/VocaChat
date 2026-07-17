import { getJson, putJson } from '@/api/http'
import type {
  AiAccountAutonomySettingsResponse,
  AutonomousInteractionSettingsResponse,
  UpdateAiAccountAutonomySettingsRequest,
  UpdateAutonomousInteractionSettingsRequest,
} from '@/api/types'

const autonomousInteractionsPath = '/api/settings/autonomous-interactions'

export function getAutonomousInteractionSettings(): Promise<AutonomousInteractionSettingsResponse> {
  return getJson(autonomousInteractionsPath)
}

export function updateAutonomousInteractionSettings(
  request: UpdateAutonomousInteractionSettingsRequest,
): Promise<AutonomousInteractionSettingsResponse> {
  return putJson(autonomousInteractionsPath, request)
}

export function getAiAccountAutonomySettings(
  aiAccountId: string,
): Promise<AiAccountAutonomySettingsResponse> {
  return getJson(`/api/ai-accounts/${aiAccountId}/autonomy-settings`)
}

export function updateAiAccountAutonomySettings(
  aiAccountId: string,
  request: UpdateAiAccountAutonomySettingsRequest,
): Promise<AiAccountAutonomySettingsResponse> {
  return putJson(`/api/ai-accounts/${aiAccountId}/autonomy-settings`, request)
}
