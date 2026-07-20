import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  getAiAccountAutonomySettings,
  getAiInteractionDiagnosticLogs,
  getAutonomousInteractionSettings,
  updateAiAccountAutonomySettings,
  updateAutonomousInteractionSettings,
} from '@/api/settings'
import {
  getAiRelationship,
  updateAiRelationship,
} from '@/api/relationships'
import {
  evaluateAutonomousPrivateChat,
  runAutonomousPrivateChat,
} from '@/api/autonomousInteractions'
import type { ContactResponse } from '@/api/types'
import { AutonomousInteractionSettingsPage } from '@/components/settings/AutonomousInteractionSettingsPage'

vi.mock('@/api/settings', () => ({
  getAutonomousInteractionSettings: vi.fn(),
  updateAutonomousInteractionSettings: vi.fn(),
  getAiAccountAutonomySettings: vi.fn(),
  getAiInteractionDiagnosticLogs: vi.fn(),
  updateAiAccountAutonomySettings: vi.fn(),
}))

vi.mock('@/api/relationships', () => ({
  getAiRelationship: vi.fn(),
  updateAiRelationship: vi.fn(),
}))

vi.mock('@/api/autonomousInteractions', () => ({
  evaluateAutonomousPrivateChat: vi.fn(),
  runAutonomousPrivateChat: vi.fn(),
  evaluateAutonomousGroupChat: vi.fn(),
  runAutonomousGroupChat: vi.fn(),
}))

const getSettingsMock = vi.mocked(getAutonomousInteractionSettings)
const updateSettingsMock = vi.mocked(updateAutonomousInteractionSettings)
const getFriendSettingsMock = vi.mocked(getAiAccountAutonomySettings)
const getInteractionLogsMock = vi.mocked(getAiInteractionDiagnosticLogs)
const updateFriendSettingsMock = vi.mocked(updateAiAccountAutonomySettings)
const getRelationshipMock = vi.mocked(getAiRelationship)
const updateRelationshipMock = vi.mocked(updateAiRelationship)
const evaluatePrivateChatMock = vi.mocked(evaluateAutonomousPrivateChat)
const runPrivateChatMock = vi.mocked(runAutonomousPrivateChat)

const friendId = '10000000-0000-0000-0000-000000000001'
const secondFriendId = '10000000-0000-0000-0000-000000000002'
const contacts: ContactResponse[] = [
  {
    id: '20000000-0000-0000-0000-000000000001',
    contactGroupId: '30000000-0000-0000-0000-000000000001',
    contactGroupName: '我的好友',
    createdAt: '2026-07-17T12:00:00Z',
    friend: {
      id: friendId,
      vcNumber: '1000001',
      nickname: '林澈',
      identityDescription: '摄影师',
      personality: '沉静',
      speakingStyle: '简洁',
      signature: '在城市里收集光影。',
      birthday: null,
      age: null,
      zodiacSign: null,
      gender: 'Unspecified',
      location: '杭州',
      occupation: '摄影师',
      hometown: '苏州',
      onlineStatus: 'Online',
      avatarUrl: null,
      coverUrl: null,
      interestTags: ['摄影'],
      personalityTags: ['安静'],
      createdAt: '2026-07-17T12:00:00Z',
    },
  },
  {
    id: '20000000-0000-0000-0000-000000000002',
    contactGroupId: '30000000-0000-0000-0000-000000000001',
    contactGroupName: '我的好友',
    createdAt: '2026-07-17T12:01:00Z',
    friend: {
      id: secondFriendId,
      vcNumber: '1000002',
      nickname: '周野',
      identityDescription: '编辑',
      personality: '坦率',
      speakingStyle: '直接',
      signature: '认真生活。',
      birthday: null,
      age: null,
      zodiacSign: null,
      gender: 'Unspecified',
      location: '上海',
      occupation: '编辑',
      hometown: '南京',
      onlineStatus: 'Online',
      avatarUrl: null,
      coverUrl: null,
      interestTags: ['阅读'],
      personalityTags: ['坦率'],
      createdAt: '2026-07-17T12:01:00Z',
    },
  },
]

describe('AutonomousInteractionSettingsPage', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/')
    getSettingsMock.mockResolvedValue({
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
    })
    updateSettingsMock.mockImplementation(async (request) => request)
    getFriendSettingsMock.mockResolvedValue({
      aiAccountId: friendId,
      isEnabled: true,
      initiativeLevel: 'Normal',
      canInitiatePrivateChats: true,
      canInitiateGroupChats: true,
      canJoinGroupChats: true,
      useGlobalReplyDelay: true,
      replyDelayMode: 'RandomRange',
      fixedReplyDelayMilliseconds: 1200,
      minimumReplyDelayMilliseconds: 800,
      maximumReplyDelayMilliseconds: 1800,
      useGlobalConsecutiveMessageDelay: true,
      consecutiveMessageDelayMode: 'RandomRange',
      fixedConsecutiveMessageDelayMilliseconds: 700,
      minimumConsecutiveMessageDelayMilliseconds: 400,
      maximumConsecutiveMessageDelayMilliseconds: 1200,
      useGlobalQuestionPolicy: true,
      maximumConsecutiveQuestionTurns: 2,
    })
    updateFriendSettingsMock.mockImplementation(async (accountId, request) => ({
      aiAccountId: accountId,
      ...request,
    }))
    getInteractionLogsMock.mockResolvedValue([])
    getRelationshipMock.mockImplementation(async (fromAiAccountId, toAiAccountId) => ({
      fromAiAccountId,
      toAiAccountId,
      familiarity: fromAiAccountId === friendId ? 10 : 30,
      affinity: fromAiAccountId === friendId ? 0 : 15,
      trust: fromAiAccountId === friendId ? 10 : 25,
      interactionCount: 0,
      lastInteractionAt: null,
      updatedAt: null,
    }))
    updateRelationshipMock.mockImplementation(async (fromAiAccountId, toAiAccountId, request) => ({
      fromAiAccountId,
      toAiAccountId,
      ...request,
      interactionCount: 0,
      lastInteractionAt: null,
      updatedAt: '2026-07-17T12:30:00Z',
    }))
    evaluatePrivateChatMock.mockResolvedValue({
      isApproved: true,
      stage: 'Approved',
      interactionType: 'PrivateChat',
      firstAiAccountId: friendId,
      secondAiAccountId: secondFriendId,
      initiatorAiAccountId: friendId,
      recipientAiAccountId: secondFriendId,
      relationshipScore: 72,
      initiativeAdjustment: 10,
      randomJitter: -2,
      finalScore: 80,
      threshold: 50,
      cooldownEndsAt: null,
    })
    runPrivateChatMock.mockResolvedValue({
      status: 'Completed',
      decision: {
        isApproved: true,
        stage: 'Approved',
        interactionType: 'PrivateChat',
        firstAiAccountId: friendId,
        secondAiAccountId: secondFriendId,
        initiatorAiAccountId: friendId,
        recipientAiAccountId: secondFriendId,
        relationshipScore: 72,
        initiativeAdjustment: 10,
        randomJitter: 1,
        finalScore: 83,
        threshold: 50,
        cooldownEndsAt: null,
      },
      privateChat: {
        id: '40000000-0000-0000-0000-000000000001',
        category: 'FriendPrivateChat',
        contactId: null,
        friend: null,
        participants: [contacts[0].friend, contacts[1].friend],
        createdAt: '2026-07-18T12:00:00Z',
      },
      privateChatCreated: true,
      session: {
        id: '60000000-0000-0000-0000-000000000001',
        privateChatId: '40000000-0000-0000-0000-000000000001',
        initiatorAiAccountId: friendId,
        recipientAiAccountId: secondFriendId,
        topic: '摄影',
        maximumRounds: 6,
        continuationRatePercent: 80,
        completedRounds: 2,
        status: 'Completed',
        endReason: 'ContinuationProbabilityDeclined',
        startedAt: '2026-07-18T12:00:00Z',
        lastActivityAt: '2026-07-18T12:00:00.0000001Z',
        endedAt: '2026-07-18T12:00:00.0000001Z',
      },
      rounds: [
        {
          id: '70000000-0000-0000-0000-000000000001',
          roundNumber: 1,
          isClosing: false,
          occurrenceProbability: 1,
          randomRoll: null,
          initiatorMessageMode: 'Single',
          recipientMessageMode: 'Single',
          initiatorMessageCount: 1,
          recipientMessageCount: 1,
          startedAt: '2026-07-18T12:00:00Z',
          completedAt: '2026-07-18T12:00:00.0000002Z',
        },
        {
          id: '70000000-0000-0000-0000-000000000002',
          roundNumber: 2,
          isClosing: true,
          occurrenceProbability: null,
          randomRoll: null,
          initiatorMessageMode: 'None',
          recipientMessageMode: 'None',
          initiatorMessageCount: 0,
          recipientMessageCount: 0,
          startedAt: '2026-07-18T12:00:00.0000003Z',
          completedAt: '2026-07-18T12:00:00.0000003Z',
        },
      ],
      messages: [
        {
          id: '50000000-0000-0000-0000-000000000001',
          sequenceNumber: 1,
          privateChatId: '40000000-0000-0000-0000-000000000001',
          senderType: 'AiAccount',
          senderDisplayName: '林澈',
          senderAiAccountId: friendId,
          senderAvatarUrl: null,
          content: '周野，刚好想到你了。',
          sentAt: '2026-07-18T12:00:00Z',
        },
        {
          id: '50000000-0000-0000-0000-000000000002',
          sequenceNumber: 2,
          privateChatId: '40000000-0000-0000-0000-000000000001',
          senderType: 'AiAccount',
          senderDisplayName: '周野',
          senderAiAccountId: secondFriendId,
          senderAvatarUrl: null,
          content: '林澈，我也正想和你聊聊。',
          sentAt: '2026-07-18T12:00:00.0000001Z',
        },
      ],
      errorMessage: null,
    })
  })

  it('读取通用设置、启用自主互动并保存语义频率', async () => {
    const user = userEvent.setup()
    render(<AutonomousInteractionSettingsPage />)

    const masterSwitch = await screen.findByRole('switch', {
      name: '允许好友自主互动',
    })
    expect(masterSwitch).toHaveAttribute('aria-checked', 'false')

    await user.click(masterSwitch)
    await user.click(screen.getByRole('radio', { name: '高' }))
    await user.click(screen.getByRole('button', { name: '保存更改' }))

    await waitFor(() => {
      expect(updateSettingsMock).toHaveBeenCalledWith({
        isEnabled: true,
        frequency: 'High',
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
      })
    })
    expect(screen.getByText('设置已保存到本地数据库。')).toBeInTheDocument()
  })

  it('保存下一轮概率保留比例和单次硬上限', async () => {
    const user = userEvent.setup()
    render(<AutonomousInteractionSettingsPage />)

    const continuationInput = await screen.findByLabelText('下一轮概率保留比例')
    const maximumRoundsInput = screen.getByLabelText('单次私信最大轮数')
    await user.click(screen.getByRole('switch', { name: '允许好友自主互动' }))
    await user.clear(continuationInput)
    await user.type(continuationInput, '72')
    await user.clear(maximumRoundsInput)
    await user.type(maximumRoundsInput, '9')
    await user.click(screen.getByRole('button', { name: '保存更改' }))

    await waitFor(() => {
      expect(updateSettingsMock).toHaveBeenCalledWith({
        isEnabled: true,
        frequency: 'Normal',
        allowPrivateChats: true,
        allowGroupChats: true,
        privateChatContinuationRatePercent: 72,
        privateChatMaximumRounds: 9,
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
      })
    })
  })

  it('保存好友群聊人数、概率和轮数限制', async () => {
    const user = userEvent.setup()
    render(<AutonomousInteractionSettingsPage />)

    await user.click(await screen.findByRole('switch', {
      name: '允许好友自主互动',
    }))
    const maximumMembersInput = screen.getByLabelText('单次好友群聊最大人数')
    const continuationInput = screen.getByLabelText('好友群聊下一轮概率保留比例')
    const maximumRoundsInput = screen.getByLabelText('单次好友群聊最大轮数')
    await user.clear(maximumMembersInput)
    await user.type(maximumMembersInput, '12')
    await user.clear(continuationInput)
    await user.type(continuationInput, '65')
    await user.clear(maximumRoundsInput)
    await user.type(maximumRoundsInput, '8')
    await user.click(screen.getByRole('button', { name: '保存更改' }))

    await waitFor(() => {
      expect(updateSettingsMock).toHaveBeenCalledWith({
        isEnabled: true,
        frequency: 'Normal',
        allowPrivateChats: true,
        allowGroupChats: true,
        privateChatContinuationRatePercent: 80,
        privateChatMaximumRounds: 6,
        autonomousGroupChatMaximumMembers: 12,
        groupChatContinuationRatePercent: 65,
        groupChatMaximumRounds: 8,
        replyDelayMode: 'RandomRange',
        fixedReplyDelayMilliseconds: 1200,
        minimumReplyDelayMilliseconds: 800,
        maximumReplyDelayMilliseconds: 1800,
        consecutiveMessageDelayMode: 'RandomRange',
        fixedConsecutiveMessageDelayMilliseconds: 700,
        minimumConsecutiveMessageDelayMilliseconds: 400,
        maximumConsecutiveMessageDelayMilliseconds: 1200,
        maximumConsecutiveQuestionTurns: 2,
      })
    })
  })

  it('随机回复区间由用户决定且最长等待输入没有产品上限', async () => {
    const user = userEvent.setup()
    render(<AutonomousInteractionSettingsPage />)

    const maximumDelayInput = await screen.findByLabelText('回复等待：最长等待')
    expect(maximumDelayInput).not.toHaveAttribute('max')
    await user.clear(maximumDelayInput)
    await user.type(maximumDelayInput, '98765.4')
    await user.click(screen.getByRole('button', { name: '保存更改' }))

    await waitFor(() => {
      expect(updateSettingsMock).toHaveBeenCalledWith(expect.objectContaining({
        replyDelayMode: 'RandomRange',
        minimumReplyDelayMilliseconds: 800,
        maximumReplyDelayMilliseconds: 98765400,
      }))
    })
  })

  it('选择好友后读取并保存好友专有设置', async () => {
    const user = userEvent.setup()
    render(
      <AutonomousInteractionSettingsPage
        contacts={contacts}
        contactStatus="success"
      />,
    )

    await user.click(screen.getByRole('tab', { name: '好友设置' }))

    expect(await screen.findByText('好友专有设置')).toBeInTheDocument()
    expect(getFriendSettingsMock).toHaveBeenCalledWith(friendId)

    await user.click(screen.getByRole('radio', { name: '高' }))
    await user.click(screen.getByRole('switch', { name: '允许主动创建群聊' }))
    await user.click(screen.getByRole('button', { name: '保存更改' }))

    await waitFor(() => {
      expect(updateFriendSettingsMock).toHaveBeenCalledWith(friendId, {
        isEnabled: true,
        initiativeLevel: 'High',
        canInitiatePrivateChats: true,
        canInitiateGroupChats: false,
        canJoinGroupChats: true,
        useGlobalReplyDelay: true,
        replyDelayMode: 'RandomRange',
        fixedReplyDelayMilliseconds: 1200,
        minimumReplyDelayMilliseconds: 800,
        maximumReplyDelayMilliseconds: 1800,
        useGlobalConsecutiveMessageDelay: true,
        consecutiveMessageDelayMode: 'RandomRange',
        fixedConsecutiveMessageDelayMilliseconds: 700,
        minimumConsecutiveMessageDelayMilliseconds: 400,
        maximumConsecutiveMessageDelayMilliseconds: 1200,
        useGlobalQuestionPolicy: true,
        maximumConsecutiveQuestionTurns: 2,
      })
    })
  })

  it('加载失败时显示可重试错误状态', async () => {
    getSettingsMock.mockRejectedValueOnce(new Error('无法连接测试 API'))
    const user = userEvent.setup()
    render(<AutonomousInteractionSettingsPage />)

    expect(await screen.findByText('无法连接到 VocaChat Web API')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: '重新加载' }))

    expect(await screen.findByRole('switch', { name: '允许好友自主互动' })).toBeInTheDocument()
  })

  it('互动日志集中显示生成问题，不把内部错误放进聊天区', async () => {
    getInteractionLogsMock.mockResolvedValueOnce([
      {
        id: '80000000-0000-0000-0000-000000000001',
        occurredAt: '2026-07-20T03:30:00Z',
        severity: 'Error',
        code: 'MessageGenerationFailed',
        scenario: 'PrivatePrimaryReply',
        aiAccountId: friendId,
        conversationId: '40000000-0000-0000-0000-000000000001',
        summary: '私信回复生成失败，用户消息已经保存。',
        detail: '模型输出没有满足本轮表达计划。',
        wasRecovered: false,
      },
    ])
    const user = userEvent.setup()
    render(<AutonomousInteractionSettingsPage />)

    await user.click(screen.getByRole('tab', { name: '互动日志' }))

    expect(await screen.findByText('私信回复生成失败，用户消息已经保存。')).toBeInTheDocument()
    expect(screen.getByText('模型输出没有满足本轮表达计划。')).toBeInTheDocument()
    expect(getInteractionLogsMock).toHaveBeenCalledWith()
  })

  it('按方向读取关系、显示反向摘要并保存关系数值', async () => {
    const user = userEvent.setup()
    render(
      <AutonomousInteractionSettingsPage
        contacts={contacts}
        contactStatus="success"
      />,
    )

    await user.click(screen.getByRole('tab', { name: '关系设置' }))

    expect(await screen.findByRole('heading', { name: '林澈 对 周野' })).toBeInTheDocument()
    expect(getRelationshipMock).toHaveBeenCalledWith(friendId, secondFriendId)
    expect(getRelationshipMock).toHaveBeenCalledWith(secondFriendId, friendId)

    await user.click(screen.getByRole('button', { name: '预览私信判断' }))
    await waitFor(() => {
      expect(evaluatePrivateChatMock).toHaveBeenCalledWith({
        firstAiAccountId: friendId,
        secondAiAccountId: secondFriendId,
      })
    })
    expect(await screen.findByText('可以发起')).toBeInTheDocument()
    expect(screen.getByText('判断发起方：', { exact: false })).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('熟悉度'), {
      target: { value: '55' },
    })
    expect(screen.getByRole('button', { name: '重新判断' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: '保存更改' }))

    await waitFor(() => {
      expect(updateRelationshipMock).toHaveBeenCalledWith(
        friendId,
        secondFriendId,
        { familiarity: 55, affinity: 0, trust: 10 },
      )
    })
  })

  it('明确触发一次好友私信并可进入新会话', async () => {
    const user = userEvent.setup()
    const onOpenPrivateChat = vi.fn()
    render(
      <AutonomousInteractionSettingsPage
        contacts={contacts}
        contactStatus="success"
        onOpenPrivateChat={onOpenPrivateChat}
      />,
    )

    await user.click(screen.getByRole('tab', { name: '关系设置' }))
    await screen.findByRole('heading', { name: '林澈 对 周野' })
    await user.type(await screen.findByLabelText(/本次话题/), '最近看的电影')
    await user.click(screen.getByRole('button', { name: '尝试发起一次私信' }))

    await waitFor(() => {
      expect(runPrivateChatMock).toHaveBeenCalledWith({
        firstAiAccountId: friendId,
        secondAiAccountId: secondFriendId,
        topic: '最近看的电影',
      })
    })
    expect(await screen.findByText('好友已完成 2 轮私信交流')).toBeInTheDocument()
    expect(screen.getByText('已完成 2 / 6 轮')).toBeInTheDocument()
    expect(screen.getByText('下一轮概率未通过')).toBeInTheDocument()
    expect(screen.getByText('周野，刚好想到你了。')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '查看好友私信' }))
    expect(onOpenPrivateChat).toHaveBeenCalledWith(
      '40000000-0000-0000-0000-000000000001',
    )
  })
})
