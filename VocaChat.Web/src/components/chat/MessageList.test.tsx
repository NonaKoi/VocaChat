import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { ChatMessageResponse } from '@/api/types'
import { MessageList } from '@/components/chat/MessageList'

describe('MessageList', () => {
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
    senderType,
    senderDisplayName,
    senderAiAccountId,
    senderAvatarUrl: null,
    content,
    sentAt: `2026-07-19T10:0${id}:00`,
  }
}
