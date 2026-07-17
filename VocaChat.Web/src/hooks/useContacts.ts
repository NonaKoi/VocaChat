import { useCallback } from 'react'
import { getContactGroups, getContacts } from '@/api/contacts'
import { useRemoteCollection } from '@/hooks/useRemoteCollection'

export function useContacts() {
  const loadContacts = useCallback(() => getContacts(), [])
  const loadGroups = useCallback(() => getContactGroups(), [])
  const contacts = useRemoteCollection(loadContacts)
  const groups = useRemoteCollection(loadGroups)
  return { ...contacts, groups: groups.data, reloadGroups: groups.reload }
}
