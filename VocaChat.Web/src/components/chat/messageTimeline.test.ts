import { describe, expect, it } from 'vitest'
import type { GroupMessageResponse } from '@/api/types'
import { buildMessageTimeline } from '@/components/chat/messageTimeline'

describe('buildMessageTimeline', () => {
  it('按日期分隔消息，并合并五分钟内同一发送者的连续消息', () => {
    const messages = [
      createMessage('1', 'AiAccount', '小语', 'ai-1', '2026-07-16T10:00:00'),
      createMessage('2', 'AiAccount', '小语', 'ai-1', '2026-07-16T10:03:00'),
      createMessage('3', 'User', '我', null, '2026-07-16T10:04:00'),
      createMessage('4', 'User', '我', null, '2026-07-17T09:00:00'),
    ]

    const timeline = buildMessageTimeline(messages, new Date('2026-07-17T12:00:00'))

    expect(timeline.map((item) => item.kind)).toEqual([
      'date-divider',
      'message-group',
      'message-group',
      'date-divider',
      'message-group',
    ])
    expect(timeline[0]).toMatchObject({ label: '昨天' })
    expect(timeline[1]).toMatchObject({
      senderDisplayName: '小语',
      messages: [{ id: '1' }, { id: '2' }],
    })
    expect(timeline[3]).toMatchObject({ label: '今天' })
  })

  it('同一发送者间隔超过五分钟时创建新分组', () => {
    const messages = [
      createMessage('1', 'AiAccount', '小语', 'ai-1', '2026-07-17T10:00:00'),
      createMessage('2', 'AiAccount', '小语', 'ai-1', '2026-07-17T10:06:00'),
    ]

    const groups = buildMessageTimeline(messages).filter(
      (item) => item.kind === 'message-group',
    )

    expect(groups).toHaveLength(2)
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
    groupChatId: 'group-1',
    senderType,
    senderDisplayName,
    senderAiAccountId,
    content: `消息 ${id}`,
    sentAt,
  }
}
