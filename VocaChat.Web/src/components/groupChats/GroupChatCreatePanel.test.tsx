import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { ContactResponse, CreateGroupChatRequest } from '@/api/types'
import { GroupChatCreatePanel } from '@/components/groupChats/GroupChatCreatePanel'

const contacts: ContactResponse[] = [
  createContact('contact-1', 'account-1', '小语', 'VC-1001'),
  createContact('contact-2', 'account-2', '阿岚', 'VC-1002'),
]

describe('GroupChatCreatePanel', () => {
  it('提交群名、参与关系和所选好友', async () => {
    const user = userEvent.setup()
    const onCreate = vi.fn(async (_request: CreateGroupChatRequest) => ({ id: 'group-1' }))
    renderPanel(onCreate)

    await user.type(screen.getByLabelText('群聊名称'), '周末电影会')
    await user.click(screen.getByRole('radio', { name: /好友群聊/ }))
    await user.click(screen.getByRole('checkbox', { name: /小语/ }))
    await user.click(screen.getByRole('checkbox', { name: /阿岚/ }))
    await user.click(screen.getByRole('button', { name: '创建并进入群聊' }))

    expect(onCreate).toHaveBeenCalledWith({
      name: '周末电影会',
      includesLocalUser: false,
      memberAiAccountIds: ['account-1', 'account-2'],
    })
  })

  it('没有选择好友时显示明确验证信息', async () => {
    const user = userEvent.setup()
    const onCreate = vi.fn(async (_request: CreateGroupChatRequest) => undefined)
    renderPanel(onCreate)

    await user.type(screen.getByLabelText('群聊名称'), '空群')
    await user.click(screen.getByRole('button', { name: '创建并进入群聊' }))

    expect(screen.getByRole('alert')).toHaveTextContent('请至少选择一位好友。')
    expect(onCreate).not.toHaveBeenCalled()
  })
})

function renderPanel(
  onCreate: (request: CreateGroupChatRequest) => Promise<unknown>,
) {
  render(
    <GroupChatCreatePanel
      contacts={contacts}
      contactStatus="success"
      isSubmitting={false}
      onRetryContacts={vi.fn()}
      onCancel={vi.fn()}
      onCreate={onCreate}
    />,
  )
}

function createContact(
  id: string,
  friendId: string,
  nickname: string,
  vcNumber: string,
): ContactResponse {
  return {
    id,
    contactGroupId: 'default',
    contactGroupName: '默认分组',
    createdAt: '2026-07-18T10:00:00',
    friend: {
      id: friendId,
      vcNumber,
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
      onlineStatus: 'Offline',
      avatarUrl: null,
      coverUrl: null,
      characterWorldId: 'world-default',
      characterWorld: {
        id: 'world-default',
        name: '现实世界',
        description: '采用现代现实社会的基本规则。',
        createdAt: '2026-07-17T10:00:00Z',
        updatedAt: '2026-07-17T10:00:00Z',
      },
      interestTags: [],
      personalityTags: [],
      createdAt: '2026-07-18T10:00:00',
    },
  }
}
