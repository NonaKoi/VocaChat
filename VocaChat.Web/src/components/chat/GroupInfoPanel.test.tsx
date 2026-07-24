import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { ContactResponse, GroupChatResponse } from '@/api/types'
import { GroupInfoPanel } from '@/components/chat/GroupInfoPanel'

const groupChat: GroupChatResponse = {
  id: 'group-1',
  name: '学习群',
  includesLocalUser: true,
  createdAt: '2026-07-18T10:00:00',
  members: [{ id: 'account-1', nickname: '小语', avatarUrl: null }],
}

describe('GroupInfoPanel', () => {
  it('显示本地用户并只允许添加尚未入群的好友', async () => {
    const user = userEvent.setup()
    const onAddMember = vi.fn().mockResolvedValue(true)
    render(
      <GroupInfoPanel
        groupChat={groupChat}
        contacts={[
          createContact('contact-1', 'account-1', '小语'),
          createContact('contact-2', 'account-2', '阿岚'),
        ]}
        contactStatus="success"
        isAddingMember={false}
        onClose={vi.fn()}
        onRetryContacts={vi.fn()}
        onClearMemberError={vi.fn()}
        onAddMember={onAddMember}
      />,
    )

    expect(screen.getAllByText('我')).toHaveLength(2)
    expect(screen.getByText('2 位成员')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '添加群成员' }))

    expect(screen.queryByRole('button', { name: '添加 小语 到群聊' }))
      .not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: '添加 阿岚 到群聊' }))
    expect(onAddMember).toHaveBeenCalledWith('account-2')
  })
})

function createContact(id: string, friendId: string, nickname: string): ContactResponse {
  return {
    id,
    contactGroupId: 'default',
    contactGroupName: '默认分组',
    createdAt: '2026-07-18T10:00:00',
    friend: {
      id: friendId,
      vcNumber: `VC-${friendId}`,
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
