export interface AiModelConnectionDraft {
  baseUrl: string
  model: string
  apiKey: string
  clearApiKey: boolean
}

/** 仅用于提交按钮状态；Service 仍负责最终业务验证。 */
export function isAiModelConnectionValid(
  draft: AiModelConnectionDraft,
  disabled = false,
) {
  if (disabled) return true
  if (!draft.baseUrl.trim() || !draft.model.trim()) return false

  try {
    const url = new URL(draft.baseUrl)
    return (url.protocol === 'http:' || url.protocol === 'https:')
      && !url.search
      && !url.hash
  } catch {
    return false
  }
}
