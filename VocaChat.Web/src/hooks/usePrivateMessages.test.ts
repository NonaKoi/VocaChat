import { act, renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type {
  PrivateChatResponse,
  PrivateMessageResponse,
  SendPrivateMessageResponse,
} from '@/api/types'
import { usePrivateMessages } from '@/hooks/usePrivateMessages'

const apiMocks = vi.hoisted(() => ({
  getPrivateChat: vi.fn(),
  getPrivateMessages: vi.fn(),
  getSavedPrivateUserMessage: vi.fn(),
  sendPrivateMessage: vi.fn(),
}))

vi.mock('@/api/privateChats', () => apiMocks)

describe('usePrivateMessages', () => {
  beforeEach(() => {
    apiMocks.getPrivateChat.mockReset().mockResolvedValue(createPrivateChat())
    apiMocks.getPrivateMessages.mockReset().mockResolvedValue([])
    apiMocks.getSavedPrivateUserMessage.mockReset().mockReturnValue(undefined)
    apiMocks.sendPrivateMessage.mockReset()
  })

  it('一次用户私信成功后保留多条独立好友回复', async () => {
    apiMocks.sendPrivateMessage.mockResolvedValue(createSendResponse())
    const { result } = renderHook(() => usePrivateMessages('private-1'))
    await waitFor(() => expect(result.current.status).toBe('success'))

    let outcome = 'rejected'
    await act(async () => {
      outcome = await result.current.send('具体讲讲吧')
    })

    expect(outcome).toBe('success')
    expect(result.current.data.map((message) => message.id)).toEqual([
      'user-1',
      'ai-1',
      'ai-2',
    ])
  })
})

function createPrivateChat(): PrivateChatResponse {
  return {
    id: 'private-1',
    category: 'MyPrivateChat',
    contactId: null,
    friend: null,
    participants: [],
    createdAt: '2026-07-19T00:00:00Z',
  }
}

function createSendResponse(): SendPrivateMessageResponse {
  return {
    userMessage: createMessage('user-1', 'User', 1),
    aiReplies: [
      createMessage('ai-1', 'AiAccount', 2),
      createMessage('ai-2', 'AiAccount', 3),
    ],
  }
}

function createMessage(
  id: string,
  senderType: 'User' | 'AiAccount',
  second: number,
): PrivateMessageResponse {
  return {
    id,
    privateChatId: 'private-1',
    senderType,
    senderDisplayName: senderType === 'User' ? '我' : '小语',
    senderAiAccountId: senderType === 'User' ? null : 'friend-account',
    senderAvatarUrl: null,
    content: `${id} content`,
    sentAt: `2026-07-19T00:00:0${second}Z`,
  }
}
