import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ComponentProps } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AiAccountResponse } from '@/api/types'
import { AccountProfileSettingsPage } from '@/components/settings/AccountProfileSettingsPage'

vi.mock('@/hooks/useCharacterWorlds', () => ({
  useCharacterWorlds: () => ({
    data: [
      {
        id: 'world-default',
        name: '现实世界',
        description: '采用现代现实社会的基本规则。',
        createdAt: '2026-07-17T10:00:00Z',
        updatedAt: '2026-07-17T10:00:00Z',
      },
      {
        id: 'world-academy',
        name: '基沃托斯',
        description: '用户定义的学园都市。',
        createdAt: '2026-07-18T10:00:00Z',
        updatedAt: '2026-07-18T10:00:00Z',
      },
    ],
    status: 'success',
    isCreating: false,
    reload: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    clearMutationError: vi.fn(),
  }),
}))

const firstAccount = createAccount('account-1', '林澈', '1000001')
const secondAccount = createAccount('account-2', '周野', '1000002')

describe('AccountProfileSettingsPage', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/')
  })

  it('编辑资料后通过统一账号更新入口保存', async () => {
    const user = userEvent.setup()
    const updateAccount = vi.fn().mockImplementation(async (_id, request) => ({
      ...firstAccount,
      ...request,
    }))
    const accountChanged = vi.fn()

    renderPage({ onUpdateAccount: updateAccount, onAccountChanged: accountChanged })

    const nickname = await screen.findByLabelText('昵称')
    await user.clear(nickname)
    await user.type(nickname, '林澈（新）')
    await user.click(screen.getByRole('button', { name: '保存资料' }))

    await waitFor(() => expect(updateAccount).toHaveBeenCalledWith(
      firstAccount.id,
      expect.objectContaining({ nickname: '林澈（新）', vcNumber: '1000001' }),
    ))
    expect(accountChanged).toHaveBeenCalledOnce()
    expect(screen.getByText('账号资料已保存')).toBeInTheDocument()
  })

  it('账号切换前保护尚未保存的资料草稿', async () => {
    const user = userEvent.setup()
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    renderPage()

    const nickname = await screen.findByLabelText('昵称')
    await user.type(nickname, '未保存')
    await user.click(screen.getByRole('button', { name: /周野/ }))

    expect(confirm).toHaveBeenCalledWith('当前修改尚未保存，确定要切换吗？')
    expect(screen.getByRole('heading', { name: '林澈' })).toBeInTheDocument()

    confirm.mockReturnValue(true)
    await user.click(screen.getByRole('button', { name: /周野/ }))
    expect(await screen.findByRole('heading', { name: '周野' })).toBeInTheDocument()
  })

  it('选择角色世界后随账号资料一起保存', async () => {
    const user = userEvent.setup()
    const updateAccount = vi.fn().mockImplementation(
      async (_id, request) => ({
        ...firstAccount,
        ...request,
        characterWorld: {
          id: request.characterWorldId,
          name: '基沃托斯',
          description: '用户定义的学园都市。',
          createdAt: '2026-07-18T10:00:00Z',
          updatedAt: '2026-07-18T10:00:00Z',
        },
      }),
    )

    renderPage({ onUpdateAccount: updateAccount })

    await user.selectOptions(
      await screen.findByRole('combobox', { name: '当前角色世界' }),
      'world-academy',
    )
    await user.click(screen.getByRole('button', { name: '保存资料' }))

    await waitFor(() => expect(updateAccount).toHaveBeenCalledWith(
      firstAccount.id,
      expect.objectContaining({ characterWorldId: 'world-academy' }),
    ))
  })
})

function renderPage(overrides: Partial<ComponentProps<typeof AccountProfileSettingsPage>> = {}) {
  const props: ComponentProps<typeof AccountProfileSettingsPage> = {
    accounts: [firstAccount, secondAccount],
    accountStatus: 'success',
    onReloadAccounts: vi.fn(),
    onUpdateAccount: vi.fn(),
    onUploadAvatar: vi.fn().mockResolvedValue(true),
    onUploadCover: vi.fn().mockResolvedValue(true),
    onClearAccountErrors: vi.fn(),
    onAccountChanged: vi.fn(),
    onDirtyChange: vi.fn(),
    ...overrides,
  }
  return render(<AccountProfileSettingsPage {...props} />)
}

function createAccount(id: string, nickname: string, vcNumber: string): AiAccountResponse {
  return {
    id,
    vcNumber,
    nickname,
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
    characterWorldId: 'world-default',
    characterWorld: {
      id: 'world-default',
      name: '现实世界',
      description: '采用现代现实社会的基本规则。',
      createdAt: '2026-07-17T10:00:00Z',
      updatedAt: '2026-07-17T10:00:00Z',
    },
    interestTags: ['摄影'],
    personalityTags: ['安静'],
    createdAt: '2026-07-17T12:00:00Z',
  }
}
