import { getJson, putJson } from '@/api/http'
import type {
  AiAccountAutonomySettingsResponse,
  AutonomousInteractionSettingsResponse,
  UpdateAiAccountAutonomySettingsRequest,
  UpdateAutonomousInteractionSettingsRequest,
  AiInteractionDiagnosticLogResponse,
  AiModelConnectionSettingsResponse,
  UpdateAiModelConnectionSettingsRequest,
  AiAccountModelConnectionSettingsResponse,
  UpdateAiAccountModelConnectionSettingsRequest,
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

export function getAiModelConnectionSettings(): Promise<AiModelConnectionSettingsResponse> {
  return getJson('/api/settings/ai-model')
}

export function updateAiModelConnectionSettings(
  request: UpdateAiModelConnectionSettingsRequest,
): Promise<AiModelConnectionSettingsResponse> {
  return putJson('/api/settings/ai-model', request)
}

export function getAiAccountModelConnectionSettings(
  aiAccountId: string,
): Promise<AiAccountModelConnectionSettingsResponse> {
  return getJson(`/api/ai-accounts/${aiAccountId}/model-settings`)
}

export function updateAiAccountModelConnectionSettings(
  aiAccountId: string,
  request: UpdateAiAccountModelConnectionSettingsRequest,
): Promise<AiAccountModelConnectionSettingsResponse> {
  return putJson(`/api/ai-accounts/${aiAccountId}/model-settings`, request)
}

export function getAiInteractionDiagnosticLogs(
  limit = 100,
): Promise<AiInteractionDiagnosticLogResponse[]> {
  return getJson<AiInteractionDiagnosticLogResponse[]>(
    `/api/settings/interaction-logs?limit=${encodeURIComponent(limit)}`,
  )
}
