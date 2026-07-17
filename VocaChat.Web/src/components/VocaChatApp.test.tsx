import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { VocaChatApp } from '@/components/VocaChatApp'

const account = {
  id: 'account-1', vcNumber: 'XiaoYu#01', nickname: '小语', identityDescription: '学习伙伴', personality: '耐心', speakingStyle: '简洁温和', signature: '按自己的节奏前进。', birthday: '2000-07-23', age: 26, zodiacSign: '狮子座', gender: 'Female', location: '中国 上海', occupation: '插画师', hometown: '中国 杭州', onlineStatus: 'Online', avatarUrl: null, coverUrl: null, interestTags: ['绘画'], personalityTags: ['耐心'], createdAt: '2026-07-17T12:00:00',
}

vi.mock('@/hooks/useGroupChats', () => ({
  useGroupChats: () => ({
    data: [
      {
        id: 'group-1',
        name: '学习群',
        createdAt: '2026-07-17T12:00:00',
        members: [{ id: 'account-1', nickname: '小语', avatarUrl: null }],
      },
    ],
    status: 'success',
    reload: vi.fn(),
  }),
}))

vi.mock('@/hooks/useGroupMessages', () => ({
  useGroupMessages: () => ({
    data: [],
    status: 'success',
    isSending: false,
    reload: vi.fn(),
    send: vi.fn().mockResolvedValue('success'),
  }),
}))

vi.mock('@/hooks/useAiAccounts', () => ({
  useAiAccounts: () => ({
    data: [account],
    status: 'success',
    isCreating: false,
    reload: vi.fn(),
    create: vi.fn(),
    clearCreateError: vi.fn(),
    uploadAvatar: vi.fn(),
    uploadCover: vi.fn(),
    clearMediaUploadError: vi.fn(),
  }),
}))

vi.mock('@/hooks/useContacts', () => ({ useContacts: () => ({ data: [{ id: 'contact-1', contactGroupId: 'default', contactGroupName: '默认分组', createdAt: account.createdAt, friend: account }], groups: [{ id: 'default', name: '默认分组', sortOrder: 0, createdAt: account.createdAt }], status: 'success', reload: vi.fn(), reloadGroups: vi.fn() }) }))
vi.mock('@/hooks/useConversations', () => ({ useConversations: () => ({ data: [{ id: 'group-1', kind: 'GroupChat', displayName: '学习群', avatarUrl: null, memberCount: 1, contactId: null, latestSenderDisplayName: null, latestMessageContent: null, latestMessageAt: null, createdAt: '2026-07-17T12:00:00' }], status: 'success', reload: vi.fn() }) }))
vi.mock('@/hooks/usePrivateMessages', () => ({ usePrivateMessages: () => ({ data: [], status: 'idle', isSending: false, reload: vi.fn(), send: vi.fn() }) }))
vi.mock('@/hooks/usePosts', () => ({ usePosts: () => ({ data: [], status: 'success', reload: vi.fn(), toggleLike: vi.fn(), addComment: vi.fn() }) }))

describe('VocaChatApp', () => {
  it('切换到好友区域后仍保留聊天草稿', async () => {
    const user = userEvent.setup()
    render(<VocaChatApp />)

    await user.click(screen.getByRole('button', { name: /学习群/ }))
    await user.type(screen.getByLabelText('消息内容'), '尚未发送的草稿')
    await user.click(screen.getByRole('button', { name: '好友' }))

    expect(screen.getByRole('heading', { name: '好友' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '聊天' }))

    expect(screen.getByLabelText('消息内容')).toHaveValue('尚未发送的草稿')
  })

  it('离开寻找新朋友页面后仍保留未提交资料', async () => {
    const user = userEvent.setup()
    render(<VocaChatApp />)

    await user.click(screen.getByRole('button', { name: '好友' }))
    await user.click(
      screen.getAllByRole('button', { name: '寻找新朋友' })[0],
    )
    await user.type(screen.getByLabelText('昵称'), '尚未创建的小语')

    await user.click(screen.getByRole('button', { name: '聊天' }))
    await user.click(screen.getByRole('button', { name: '好友' }))

    expect(screen.getByLabelText('昵称')).toHaveValue('尚未创建的小语')
  })
})
