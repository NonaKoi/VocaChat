import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { ConversationSummaryResponse } from '@/api/types'
import { ConversationList } from '@/components/chat/ConversationList'

const conversations: ConversationSummaryResponse[] = [
  createConversation('my-private', 'PrivateChat', 'MyPrivateChat', '我的好友'),
  createConversation('friend-private', 'PrivateChat', 'FriendPrivateChat', '好友甲 与 好友乙'),
  createConversation('my-group', 'GroupChat', 'MyGroupChat', '我的群'),
  createConversation('friend-group', 'GroupChat', 'FriendGroupChat', '好友群'),
]

describe('ConversationList', () => {
  it('在私聊和群聊下按参与关系显示二级分类', async () => {
    const user = userEvent.setup()
    render(
      <ConversationList
        conversations={conversations}
        status="success"
        onSelect={vi.fn()}
        onRetry={vi.fn()}
      />,
    )

    await user.click(screen.getByRole('tab', { name: '私聊' }))
    expect(screen.getByRole('tab', { name: '我的私信' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(screen.getByRole('button', { name: /我的好友/ })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /好友甲 与 好友乙/ }))
      .not.toBeInTheDocument()

    await user.click(screen.getByRole('tab', { name: '好友私信' }))
    expect(screen.getByRole('button', { name: /好友甲 与 好友乙/ }))
      .toBeInTheDocument()

    await user.click(screen.getByRole('tab', { name: '群聊' }))
    expect(screen.getByRole('tab', { name: '我的群聊' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(screen.getByRole('button', { name: /我的群/ })).toBeInTheDocument()

    await user.click(screen.getByRole('tab', { name: '好友群聊' }))
    expect(screen.getByRole('button', { name: /好友群/ })).toBeInTheDocument()
  })
})

function createConversation(
  id: string,
  kind: ConversationSummaryResponse['kind'],
  category: ConversationSummaryResponse['category'],
  displayName: string,
): ConversationSummaryResponse {
  return {
    id,
    kind,
    category,
    displayName,
    avatarUrl: null,
    memberCount: kind === 'GroupChat' ? 3 : 1,
    contactId: category === 'MyPrivateChat' ? `${id}-contact` : null,
    latestSenderDisplayName: null,
    latestMessageContent: null,
    latestMessageAt: null,
    createdAt: '2026-07-17T12:00:00',
  }
}
