import { useState } from 'react'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { AiAccountResponse, CreateAiAccountRequest } from '@/api/types'
import { AiAccountCreateForm } from '@/components/aiAccounts/AiAccountCreateForm'

describe('AiAccountCreateForm', () => {
  it('提交完整好友资料', async () => {
    const user = userEvent.setup()
    const create = vi.fn().mockResolvedValue(createAccountResponse())
    render(<CreateFormHarness onCreate={create} />)

    await user.type(screen.getByLabelText('昵称'), '小语')
    await user.type(screen.getByLabelText('VC号'), 'XiaoYu#01')
    await user.type(screen.getByLabelText('生日'), '2000-07-23')
    await user.selectOptions(screen.getByLabelText('性别'), 'Female')
    await user.selectOptions(screen.getByLabelText('状态'), 'Online')
    await user.type(screen.getByLabelText('所在地'), '中国 上海')
    await user.type(screen.getByLabelText('职业'), '插画师')
    await user.type(screen.getByLabelText('故乡'), '中国 杭州')
    await user.type(screen.getByLabelText('个性签名'), '按自己的节奏前进。')
    await user.type(screen.getByLabelText('个人介绍'), '学习伙伴')
    await user.type(screen.getByLabelText('性格描述'), '耐心、好奇')
    await user.type(screen.getByLabelText('说话风格'), '简洁温和')
    await user.type(screen.getByLabelText('添加兴趣标签'), '绘画{Enter}')
    await user.type(screen.getByLabelText('添加个性标签'), '耐心{Enter}')
    await user.click(screen.getByRole('button', { name: '寻找并添加为好友' }))

    await waitFor(() =>
      expect(create).toHaveBeenCalledWith({
        nickname: '小语',
        vcNumber: 'XiaoYu#01',
        identityDescription: '学习伙伴',
        personality: '耐心、好奇',
        speakingStyle: '简洁温和',
        signature: '按自己的节奏前进。',
        birthday: '2000-07-23',
        gender: 'Female',
        location: '中国 上海',
        occupation: '插画师',
        hometown: '中国 杭州',
        onlineStatus: 'Online',
        interestTags: ['绘画'],
        personalityTags: ['耐心'],
      }),
    )
  }, 10_000)

  it('昵称只有空白时不能提交', async () => {
    const user = userEvent.setup()
    const create = vi.fn()
    render(<CreateFormHarness onCreate={create} />)

    await user.type(screen.getByLabelText('昵称'), '   ')

    expect(
      screen.getByRole('button', { name: '寻找并添加为好友' }),
    ).toBeDisabled()
    expect(create).not.toHaveBeenCalled()
  })

  it('添加失败时显示错误并保留已填写资料', async () => {
    const user = userEvent.setup()
    const create = vi.fn().mockResolvedValue(undefined)
    const { rerender } = render(<CreateFormHarness onCreate={create} />)

    await user.type(screen.getByLabelText('昵称'), '重复昵称')
    await user.click(screen.getByRole('button', { name: '寻找并添加为好友' }))

    rerender(
      <CreateFormHarness
        errorMessage="昵称已经被使用。"
        onCreate={create}
      />,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('昵称已经被使用。')
    expect(screen.getByLabelText('昵称')).toHaveValue('重复昵称')
  })
})

function CreateFormHarness({
  errorMessage,
  onCreate,
}: {
  errorMessage?: string
  onCreate: (
    request: CreateAiAccountRequest,
  ) => Promise<AiAccountResponse | undefined>
}) {
  const [values, setValues] = useState<CreateAiAccountRequest>({
    nickname: '',
    vcNumber: '',
    identityDescription: '',
    personality: '',
    speakingStyle: '',
    signature: '',
    birthday: '',
    gender: 'Unspecified',
    location: '',
    occupation: '',
    hometown: '',
    onlineStatus: 'Offline',
    interestTags: [],
    personalityTags: [],
  })

  return (
    <AiAccountCreateForm
      values={values}
      isSubmitting={false}
      errorMessage={errorMessage}
      onValuesChange={setValues}
      onCancel={vi.fn()}
      onCreate={onCreate}
    />
  )
}

function createAccountResponse(): AiAccountResponse {
  return {
    id: 'account-1',
    vcNumber: 'XiaoYu#01',
    nickname: '小语',
    identityDescription: '学习伙伴',
    personality: '耐心、好奇',
    speakingStyle: '简洁温和',
    signature: '按自己的节奏前进。',
    birthday: '2000-07-23',
    age: 26,
    zodiacSign: '狮子座',
    gender: 'Female',
    location: '中国 上海',
    occupation: '插画师',
    hometown: '中国 杭州',
    onlineStatus: 'Online',
    avatarUrl: null,
    coverUrl: null,
    interestTags: ['绘画'],
    personalityTags: ['耐心'],
    createdAt: '2026-07-17T12:00:00',
  }
}
