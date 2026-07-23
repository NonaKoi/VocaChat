import { act, renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type {
  GroupMessageResponse,
  SendGroupMessageResponse,
} from '@/api/types'
import { useGroupMessages } from '@/hooks/useGroupMessages'

const apiMocks = vi.hoisted(() => ({
  getGroupMessages: vi.fn(),
  sendGroupMessage: vi.fn(),
  getSavedGroupMessages: vi.fn(),
}))

vi.mock('@/api/groupMessages', () => apiMocks)

describe('useGroupMessages', () => {
  beforeEach(() => {
    apiMocks.getGroupMessages.mockReset().mockResolvedValue([])
    apiMocks.sendGroupMessage.mockReset()
    apiMocks.getSavedGroupMessages.mockReset().mockReturnValue([])
  })

  it('请求仍在处理时立即展示用户消息，并使用同一消息标识落库', async () => {
    let resolveRequest: ((response: SendGroupMessageResponse) => void) | undefined
    apiMocks.sendGroupMessage.mockImplementation((_, request) =>
      new Promise<SendGroupMessageResponse>((resolve) => {
        resolveRequest = () => resolve({
          ...createResponse('Complete', null, []),
          userMessage: createMessage(request.clientMessageId, 'User', 1),
        })
      }))
    const { result } = renderHook(() => useGroupMessages('group-1'))
    await waitFor(() => expect(result.current.status).toBe('success'))

    let sendPromise: Promise<string> | undefined
    act(() => {
      sendPromise = result.current.send('立即显示')
    })

    await waitFor(() => {
      expect(result.current.data).toHaveLength(1)
      expect(result.current.data[0].deliveryStatus).toBe('Sending')
    })
    const request = apiMocks.sendGroupMessage.mock.calls[0][1]
    expect(request.clientMessageId).toBe(result.current.data[0].id)

    resolveRequest?.(createResponse('Complete', null, []))
    await act(async () => { await sendPromise })
  })

  it('一次发送成功后合并用户消息和两条好友回复', async () => {
    const response = createResponse('Complete', null)
    apiMocks.sendGroupMessage.mockResolvedValue(response)
    const { result } = renderHook(() => useGroupMessages('group-1'))
    await waitFor(() => expect(result.current.status).toBe('success'))

    let outcome = 'rejected'
    await act(async () => {
      outcome = await result.current.send('大家一起说说看')
    })

    expect(outcome).toBe('success')
    expect(result.current.data.map((message) => message.id)).toEqual([
      'user-1',
      'ai-1',
      'ai-2',
    ])
    expect(result.current.sendErrorMessage).toBeUndefined()
  })

  it('后续回复部分失败时保留已保存消息并返回部分成功', async () => {
    const response = createResponse(
      'Partial',
      '第二位好友的回复保存失败。',
      [createMessage('ai-1', 'AiAccount', 2)],
    )
    apiMocks.sendGroupMessage.mockResolvedValue(response)
    const { result } = renderHook(() => useGroupMessages('group-1'))
    await waitFor(() => expect(result.current.status).toBe('success'))

    let outcome = 'rejected'
    await act(async () => {
      outcome = await result.current.send('继续讨论')
    })

    expect(outcome).toBe('partial')
    expect(result.current.data).toHaveLength(2)
    expect(result.current.sendErrorMessage).toBeUndefined()
  })
})

function createResponse(
  replyCompletion: 'Complete' | 'Partial',
  warningMessage: string | null,
  aiReplies = [
    createMessage('ai-1', 'AiAccount', 2),
    createMessage('ai-2', 'AiAccount', 3),
  ],
): SendGroupMessageResponse {
  return {
    userMessage: createMessage('user-1', 'User', 1),
    aiReplies,
    replyCompletion,
    warningMessage,
  }
}

function createMessage(
  id: string,
  senderType: 'User' | 'AiAccount',
  second: number,
): GroupMessageResponse {
  return {
    id,
    sequenceNumber: second + 1,
    groupChatId: 'group-1',
    senderType,
    senderDisplayName: senderType === 'User' ? '我' : id,
    senderAiAccountId: senderType === 'User' ? null : `${id}-account`,
    interactionBatchId: 'interaction-1',
    replyToMessageId: senderType === 'User' ? null : 'user-1',
    tokenUsage: null,
    senderAvatarUrl: null,
    content: `${id} content`,
    sentAt: `2026-07-18T00:00:0${second}Z`,
  }
}
