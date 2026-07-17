import { useCallback } from 'react'
import { getConversations } from '@/api/conversations'
import { useRemoteCollection } from '@/hooks/useRemoteCollection'

export function useConversations() {
  return useRemoteCollection(useCallback(() => getConversations(), []))
}
