import { getJson } from '@/api/http'
import type { ConversationSummaryResponse } from '@/api/types'

export function getConversations(): Promise<ConversationSummaryResponse[]> {
  return getJson<ConversationSummaryResponse[]>('/api/conversations')
}
