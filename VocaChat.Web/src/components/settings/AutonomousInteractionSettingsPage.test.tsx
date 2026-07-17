import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  getAiAccountAutonomySettings,
  getAutonomousInteractionSettings,
  updateAiAccountAutonomySettings,
  updateAutonomousInteractionSettings,
} from '@/api/settings'
import {
  getAiRelationship,
  updateAiRelationship,
} from '@/api/relationships'
import { evaluateAutonomousPrivateChat } from '@/api/autonomousInteractions'
import type { ContactResponse } from '@/api/types'
import { AutonomousInteractionSettingsPage } from '@/components/settings/AutonomousInteractionSettingsPage'

vi.mock('@/api/settings', () => ({
  getAutonomousInteractionSettings: vi.fn(),
  updateAutonomousInteractionSettings: vi.fn(),
  getAiAccountAutonomySettings: vi.fn(),
  updateAiAccountAutonomySettings: vi.fn(),
}))

vi.mock('@/api/relationships', () => ({
  getAiRelationship: vi.fn(),
  updateAiRelationship: vi.fn(),
}))

vi.mock('@/api/autonomousInteractions', () => ({
  evaluateAutonomousPrivateChat: vi.fn(),
}))

const getSettingsMock = vi.mocked(getAutonomousInteractionSettings)
const updateSettingsMock = vi.mocked(updateAutonomousInteractionSettings)
const getFriendSettingsMock = vi.mocked(getAiAccountAutonomySettings)
const updateFriendSettingsMock = vi.mocked(updateAiAccountAutonomySettings)
const getRelationshipMock = vi.mocked(getAiRelationship)
const updateRelationshipMock = vi.mocked(updateAiRelationship)
const evaluatePrivateChatMock = vi.mocked(evaluateAutonomousPrivateChat)

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
    })
    updateSettingsMock.mockImplementation(async (request) => request)
    getFriendSettingsMock.mockResolvedValue({
      aiAccountId: friendId,
      isEnabled: true,
      initiativeLevel: 'Normal',
      canInitiatePrivateChats: true,
      canInitiateGroupChats: true,
      canJoinGroupChats: true,
    })
    updateFriendSettingsMock.mockImplementation(async (accountId, request) => ({
      aiAccountId: accountId,
      ...request,
    }))
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
      })
    })
    expect(screen.getByText('设置已保存到本地数据库。')).toBeInTheDocument()
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
})
