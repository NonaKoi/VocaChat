import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ComponentProps } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AiAccountResponse } from '@/api/types'
import { AccountProfileSettingsPage } from '@/components/settings/AccountProfileSettingsPage'

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
    interestTags: ['摄影'],
    personalityTags: ['安静'],
    createdAt: '2026-07-17T12:00:00Z',
  }
}
