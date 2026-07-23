import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import type { ChatMessageResponse } from '@/api/types'
import { MessageList } from '@/components/chat/MessageList'

describe('MessageList', () => {
  beforeEach(() => {
    window.localStorage.clear()
  })

  it('将同一发送者的连续消息显示为独立消息项', () => {
    const { container } = render(
      <MessageList
        conversationId="private-chat-1"
        messages={[
          createMessage('1', '小语', 'ai-1', '第一条'),
          createMessage('2', '小语', 'ai-1', '第二条'),
        ]}
      />,
    )

    expect(container.querySelectorAll('article')).toHaveLength(2)
    expect(screen.getAllByText('小语')).toHaveLength(2)
  })

  it('按参与者身份稳定区分 AI 间私信的左右两侧', () => {
    render(
      <MessageList
        conversationId="private-chat-1"
        rightAlignedAiAccountId="ai-2"
        messages={[
          createMessage('1', '小语', 'ai-1', '左侧消息'),
          createMessage('2', '阿澄', 'ai-2', '右侧消息'),
          createMessage('3', '我', null, '本地用户消息', 'User'),
        ]}
      />,
    )

    expect(screen.getByText('左侧消息').closest('li')).toHaveClass('justify-start')
    expect(screen.getByText('右侧消息').closest('li')).toHaveClass('justify-end')
    expect(screen.getByText('本地用户消息').closest('li')).toHaveClass('justify-end')
    expect(screen.getByText('阿澄')).toBeInTheDocument()
  })

  it('启用显示设置后在 AI 消息下展示各阶段真实 Token 用量', () => {
    window.localStorage.setItem('vocachat.show-token-usage', 'true')
    const message = createMessage('1', '小语', 'ai-1', '我记得这件事。')
    message.tokenUsage = {
      groupDirector: createStageUsage(120, 20, 140),
      conversationDirector: createStageUsage(80, 10, 90),
      replyGeneration: {
        ...createStageUsage(100, 30, 130),
        cacheHitTokens: 40,
        cacheMissTokens: 60,
      },
      usageComplete: true,
      totalTokens: 360,
      interactionSharedMessageCount: 2,
      responseSharedMessageCount: 1,
    }

    render(
      <MessageList
        conversationId="group-chat-1"
        messages={[message]}
      />,
    )

    expect(screen.getByLabelText('Token 消耗明细')).toBeInTheDocument()
    expect(screen.getByText(/群级导演（本轮 2 条共享）/)).toBeInTheDocument()
    expect(screen.getByText(/单人导演/)).toBeInTheDocument()
    expect(screen.getByText(/回复生成/)).toBeInTheDocument()
    expect(screen.getByText(/缓存命中 40 \/ 未命中 60/)).toBeInTheDocument()
    expect(screen.getByText('合计 360 Token')).toBeInTheDocument()
  })
})

function createMessage(
  id: string,
  senderDisplayName: string,
  senderAiAccountId: string | null,
  content: string,
  senderType: ChatMessageResponse['senderType'] = 'AiAccount',
): ChatMessageResponse {
  return {
    id,
    sequenceNumber: Number(id),
    senderType,
    senderDisplayName,
    senderAiAccountId,
    tokenUsage: null,
    senderAvatarUrl: null,
    content,
    sentAt: `2026-07-19T10:0${id}:00`,
  }
}

function createStageUsage(
  inputTokens: number,
  outputTokens: number,
  totalTokens: number,
) {
  return {
    usageComplete: true,
    inputTokens,
    outputTokens,
    totalTokens,
    cacheHitTokens: null,
    cacheMissTokens: null,
    reasoningTokens: null,
    attemptCount: 1,
  }
}
