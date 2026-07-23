import { describe, expect, it } from 'vitest'
import type { GroupMessageResponse } from '@/api/types'
import { buildMessageTimeline } from '@/components/chat/messageTimeline'

describe('buildMessageTimeline', () => {
  it('按日期分隔消息，并保留同一发送者的每条连续消息', () => {
    const messages = [
      createMessage('1', 'AiAccount', '小语', 'ai-1', '2026-07-16T10:00:00'),
      createMessage('2', 'AiAccount', '小语', 'ai-1', '2026-07-16T10:03:00'),
      createMessage('3', 'User', '我', null, '2026-07-16T10:04:00'),
      createMessage('4', 'User', '我', null, '2026-07-17T09:00:00'),
    ]

    const timeline = buildMessageTimeline(messages, new Date('2026-07-17T12:00:00'))

    expect(timeline.map((item) => item.kind)).toEqual([
      'date-divider',
      'message',
      'message',
      'message',
      'date-divider',
      'message',
    ])
    expect(timeline[0]).toMatchObject({ label: '昨天' })
    expect(timeline[1]).toMatchObject({
      message: { id: '1', senderDisplayName: '小语' },
    })
    expect(timeline[2]).toMatchObject({
      message: { id: '2', senderDisplayName: '小语' },
    })
    expect(timeline[4]).toMatchObject({ label: '今天' })
  })
})

function createMessage(
  id: string,
  senderType: GroupMessageResponse['senderType'],
  senderDisplayName: string,
  senderAiAccountId: string | null,
  sentAt: string,
): GroupMessageResponse {
  return {
    id,
    sequenceNumber: Number(id),
    groupChatId: 'group-1',
    senderType,
    senderDisplayName,
    senderAiAccountId,
    interactionBatchId: 'interaction-1',
    replyToMessageId: senderType === 'User' ? null : 'user-1',
    tokenUsage: null,
    senderAvatarUrl: null,
    content: `消息 ${id}`,
    sentAt,
  }
}
