import { getJson, postJson, putJson } from '@/api/http'
import type { ContactGroupResponse, ContactResponse, PrivateChatResponse } from '@/api/types'

export function getContacts(): Promise<ContactResponse[]> {
  return getJson<ContactResponse[]>('/api/contacts')
}

export function getContactGroups(): Promise<ContactGroupResponse[]> {
  return getJson<ContactGroupResponse[]>('/api/contact-groups')
}

export function createContactGroup(name: string): Promise<ContactGroupResponse> {
  return postJson('/api/contact-groups', { name })
}

export function moveContact(contactId: string, contactGroupId: string): Promise<ContactResponse> {
  return putJson(`/api/contacts/${contactId}/group`, { contactGroupId })
}

export function openPrivateChat(contactId: string): Promise<PrivateChatResponse> {
  return putJson(`/api/contacts/${contactId}/private-chat`, {})
}
