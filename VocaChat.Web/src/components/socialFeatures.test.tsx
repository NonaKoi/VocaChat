import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { AiAccountResponse, ContactResponse, PostResponse } from '@/api/types'
import { ActivityFeed } from '@/components/activity/ActivityFeed'
import { ContactList } from '@/components/contacts/ContactList'

const friend: AiAccountResponse = { id: 'friend-1', vcNumber: 'Friend#01', nickname: '小语', identityDescription: '', personality: '', speakingStyle: '', signature: '一起慢慢前进。', birthday: null, age: null, zodiacSign: null, gender: 'Unspecified', location: '', occupation: '', hometown: '', onlineStatus: 'Online', avatarUrl: null, coverUrl: null, characterWorldId: 'world-default', characterWorld: { id: 'world-default', name: '现实世界', description: '采用现代现实社会的基本规则。', createdAt: '2026-07-17T10:00:00Z', updatedAt: '2026-07-17T10:00:00Z' }, interestTags: [], personalityTags: [], createdAt: '2026-07-17T12:00:00' }

describe('好友与动态页面', () => {
  it('按好友分组显示真实联系人并支持选择', async () => {
    const user = userEvent.setup()
    const onSelect = vi.fn()
    const contact: ContactResponse = { id: 'contact-1', contactGroupId: 'group-1', contactGroupName: '默认分组', createdAt: friend.createdAt, friend }
    render(<ContactList contacts={[contact]} groups={[{ id: 'group-1', name: '默认分组', sortOrder: 0, createdAt: friend.createdAt }]} status="success" selectedAccountId={undefined} isCreating={false} onSelect={onSelect} onCreate={vi.fn()} onRetry={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: /小语/ }))
    expect(onSelect).toHaveBeenCalledWith(friend.id)
    expect(screen.getByText('默认分组')).toBeInTheDocument()
  })

  it('动态点赞调用当前动态的交互入口', async () => {
    const user = userEvent.setup()
    const onToggleLike = vi.fn()
    const post: PostResponse = { id: 'post-1', authorAiAccountId: friend.id, authorNickname: friend.nickname, authorAvatarUrl: null, content: '今天一起学习。', createdAt: friend.createdAt, images: [], likeCount: 0, commentCount: 0, isLikedByLocalUser: false, recentComments: [] }
    render(<ActivityFeed posts={[post]} status="success" onRetry={vi.fn()} onToggleLike={onToggleLike} onComment={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: '0' }))
    expect(onToggleLike).toHaveBeenCalledWith(post.id, false)
  })
})
