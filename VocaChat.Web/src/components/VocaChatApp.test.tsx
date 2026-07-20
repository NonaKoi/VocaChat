import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
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
        includesLocalUser: true,
        createdAt: '2026-07-17T12:00:00',
        members: [{ id: 'account-1', nickname: '小语', avatarUrl: null }],
      },
    ],
    status: 'success',
    isCreating: false,
    create: vi.fn(),
    addMember: vi.fn(),
    clearCreateError: vi.fn(),
    clearMemberError: vi.fn(),
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
    updatingAccountId: undefined,
    updateErrorMessage: undefined,
    reload: vi.fn(),
    create: vi.fn(),
    clearCreateError: vi.fn(),
    update: vi.fn(),
    clearUpdateError: vi.fn(),
    uploadAvatar: vi.fn(),
    uploadCover: vi.fn(),
    clearMediaUploadError: vi.fn(),
  }),
}))

vi.mock('@/hooks/useContacts', () => ({ useContacts: () => ({ data: [{ id: 'contact-1', contactGroupId: 'default', contactGroupName: '默认分组', createdAt: account.createdAt, friend: account }], groups: [{ id: 'default', name: '默认分组', sortOrder: 0, createdAt: account.createdAt }], status: 'success', reload: vi.fn(), reloadGroups: vi.fn() }) }))
vi.mock('@/hooks/useConversations', () => ({ useConversations: () => ({ data: [{ id: 'group-1', kind: 'GroupChat', category: 'MyGroupChat', displayName: '学习群', avatarUrl: null, memberCount: 2, contactId: null, latestSenderDisplayName: null, latestMessageContent: null, latestMessageAt: null, createdAt: '2026-07-17T12:00:00' }], status: 'success', reload: vi.fn() }) }))
vi.mock('@/hooks/usePrivateMessages', () => ({ usePrivateMessages: () => ({ data: [], status: 'idle', isSending: false, reload: vi.fn(), send: vi.fn() }) }))
vi.mock('@/hooks/usePosts', () => ({ usePosts: () => ({ data: [], status: 'success', reload: vi.fn(), toggleLike: vi.fn(), addComment: vi.fn() }) }))
vi.mock('@/hooks/useAutonomousInteractionSettings', () => {
  const data = {
    isEnabled: false,
    frequency: 'Normal',
    allowPrivateChats: true,
    allowGroupChats: true,
    privateChatContinuationRatePercent: 80,
    privateChatMaximumRounds: 6,
    autonomousGroupChatMaximumMembers: 6,
    groupChatContinuationRatePercent: 80,
    groupChatMaximumRounds: 4,
    replyDelayMode: 'RandomRange',
    fixedReplyDelayMilliseconds: 1200,
    minimumReplyDelayMilliseconds: 800,
    maximumReplyDelayMilliseconds: 1800,
    consecutiveMessageDelayMode: 'RandomRange',
    fixedConsecutiveMessageDelayMilliseconds: 700,
    minimumConsecutiveMessageDelayMilliseconds: 400,
    maximumConsecutiveMessageDelayMilliseconds: 1200,
    maximumConsecutiveQuestionTurns: 2,
  }
  return {
    useAutonomousInteractionSettings: () => ({
      data,
      status: 'success',
      isSaving: false,
      reload: vi.fn(),
      save: vi.fn(),
    }),
  }
})

describe('VocaChatApp', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/')
  })

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

  it('从会话列表进入创建群聊流程', async () => {
    const user = userEvent.setup()
    render(<VocaChatApp />)

    await user.click(screen.getByRole('button', { name: '创建群聊' }))

    expect(screen.getByRole('heading', { name: '创建群聊' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: /我的群聊/ })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: /小语/ })).toBeInTheDocument()
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

  it('设置存在未保存更改时先确认再离开', async () => {
    const user = userEvent.setup()
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    render(<VocaChatApp />)

    await user.click(screen.getByRole('button', { name: '设置' }))
    await user.click(screen.getByRole('switch', { name: '允许好友自主互动' }))
    await user.click(screen.getByRole('button', { name: '聊天' }))

    expect(confirm).toHaveBeenCalledWith('设置尚未保存，确定要离开吗？')
    expect(screen.getByRole('heading', { name: '好友自主互动' })).toBeInTheDocument()

    confirm.mockReturnValue(true)
    await user.click(screen.getByRole('button', { name: '聊天' }))
    expect(screen.getByLabelText('会话列表')).toBeInTheDocument()
  })

  it('设置中可以进入与好友自主互动同级的账号资料编辑页', async () => {
    const user = userEvent.setup()
    render(<VocaChatApp />)

    await user.click(screen.getByRole('button', { name: '设置' }))
    await user.click(screen.getByRole('button', { name: '账号资料编辑' }))

    expect(screen.getByRole('heading', { name: '账号资料编辑' })).toBeInTheDocument()
    expect(await screen.findByLabelText('昵称')).toHaveValue('小语')
    expect(screen.getByRole('tab', { name: 'AI 记忆' })).toBeInTheDocument()
  })
})
