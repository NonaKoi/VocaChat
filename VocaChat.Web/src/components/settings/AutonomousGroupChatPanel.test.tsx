import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { runAutonomousGroupChat } from '@/api/autonomousInteractions'
import type { AiAccountResponse, ContactResponse } from '@/api/types'
import { AutonomousGroupChatPanel } from '@/components/settings/AutonomousGroupChatPanel'

vi.mock('@/api/autonomousInteractions', () => ({
  evaluateAutonomousGroupChat: vi.fn(),
  runAutonomousGroupChat: vi.fn(),
}))

const runGroupChatMock = vi.mocked(runAutonomousGroupChat)
const contacts = ['林澈', '周野', '苏晚'].map((nickname, index) =>
  createContact(index + 1, nickname))

describe('AutonomousGroupChatPanel', () => {
  beforeEach(() => {
    runGroupChatMock.mockResolvedValue({
      status: 'Completed',
      decision: {
        isApproved: true,
        stage: 'Approved',
        participantAiAccountIds: contacts.map((contact) => contact.friend.id),
        initiatorAiAccountId: contacts[0].friend.id,
        maximumMembers: 6,
        averageRelationshipScore: 88,
        weakestRelationshipScore: 82,
        sharedInterestBonus: 8,
        initiativeAdjustment: 5,
        randomJitter: 0,
        finalScore: 91,
        threshold: 70,
      },
      groupChat: {
        id: '40000000-0000-0000-0000-000000000001',
        name: '林澈、周野、苏晚的群聊',
        includesLocalUser: false,
        members: contacts.map((contact) => ({
          id: contact.friend.id,
          nickname: contact.friend.nickname,
          avatarUrl: null,
        })),
        createdAt: '2026-07-19T15:00:00Z',
      },
      groupChatCreated: true,
      session: {
        id: '50000000-0000-0000-0000-000000000001',
        groupChatId: '40000000-0000-0000-0000-000000000001',
        initiatorAiAccountId: contacts[0].friend.id,
        participantAiAccountIds: contacts.map((contact) => contact.friend.id),
        topic: '周末去哪里',
        maximumRounds: 4,
        continuationRatePercent: 80,
        completedRounds: 1,
        status: 'Completed',
        endReason: 'ContinuationProbabilityDeclined',
        startedAt: '2026-07-19T15:00:00Z',
        lastActivityAt: '2026-07-19T15:01:00Z',
        endedAt: '2026-07-19T15:01:00Z',
      },
      rounds: [
        {
          id: '51000000-0000-0000-0000-000000000001',
          roundNumber: 1,
          isClosing: false,
          occurrenceProbability: 1,
          randomRoll: null,
          plannedSpeakerCount: 3,
          plannedMessageCount: 3,
          startedAt: '2026-07-19T15:00:00Z',
          completedAt: '2026-07-19T15:00:30Z',
        },
        {
          id: '51000000-0000-0000-0000-000000000002',
          roundNumber: 2,
          isClosing: true,
          occurrenceProbability: null,
          randomRoll: null,
          plannedSpeakerCount: 0,
          plannedMessageCount: 0,
          startedAt: '2026-07-19T15:00:30Z',
          completedAt: '2026-07-19T15:00:30Z',
        },
      ],
      messages: contacts.map((contact, index) => ({
        id: `60000000-0000-0000-0000-00000000000${index + 1}`,
        sequenceNumber: index + 1,
        groupChatId: '40000000-0000-0000-0000-000000000001',
        senderType: 'AiAccount',
        senderDisplayName: contact.friend.nickname,
        senderAiAccountId: contact.friend.id,
        senderAvatarUrl: null,
        content: `${contact.friend.nickname}的群聊消息`,
        sentAt: `2026-07-19T15:00:0${index}Z`,
      })),
      errorMessage: null,
    })
  })

  it('选择三位好友后执行群聊并保留三位独立发言者', async () => {
    const user = userEvent.setup()
    const onOpenGroupChat = vi.fn()
    render(
      <AutonomousGroupChatPanel
        contacts={contacts}
        contactStatus="success"
        onReloadContacts={vi.fn()}
        onOpenGroupChat={onOpenGroupChat}
      />,
    )

    for (const checkbox of screen.getAllByRole('checkbox')) {
      await user.click(checkbox)
    }
    await user.type(screen.getByLabelText(/本次话题/), '周末去哪里')
    await user.click(screen.getByRole('button', { name: '尝试发起一次群聊' }))

    await waitFor(() => expect(runGroupChatMock).toHaveBeenCalledWith({
      participantAiAccountIds: contacts.map((contact) => contact.friend.id),
      topic: '周末去哪里',
    }))
    expect(screen.getByText('林澈的群聊消息')).toBeInTheDocument()
    expect(screen.getByText('周野的群聊消息')).toBeInTheDocument()
    expect(screen.getByText('苏晚的群聊消息')).toBeInTheDocument()
    expect(screen.getByText(/已完成 1 轮交流/)).toBeInTheDocument()
    expect(screen.getByText('下一轮未发生')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /查看好友群聊/ }))
    expect(onOpenGroupChat).toHaveBeenCalledWith(
      '40000000-0000-0000-0000-000000000001',
    )
  })
})

function createContact(index: number, nickname: string): ContactResponse {
  const accountId = `10000000-0000-0000-0000-00000000000${index}`
  const friend: AiAccountResponse = {
    id: accountId,
    vcNumber: `100000${index}`,
    nickname,
    identityDescription: '',
    personality: '',
    speakingStyle: '',
    signature: '',
    birthday: null,
    age: null,
    zodiacSign: null,
    gender: 'Unspecified',
    location: '',
    occupation: '',
    hometown: '',
    onlineStatus: 'Online',
    avatarUrl: null,
    coverUrl: null,
    interestTags: [],
    personalityTags: [],
    createdAt: '2026-07-19T12:00:00Z',
  }
  return {
    id: `20000000-0000-0000-0000-00000000000${index}`,
    contactGroupId: '30000000-0000-0000-0000-000000000001',
    contactGroupName: '我的好友',
    friend,
    createdAt: '2026-07-19T12:00:00Z',
  }
}
