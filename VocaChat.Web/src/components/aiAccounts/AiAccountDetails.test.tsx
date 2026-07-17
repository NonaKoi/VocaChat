import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { AiAccountResponse } from '@/api/types'
import { AiAccountDetails } from '@/components/aiAccounts/AiAccountDetails'

describe('AiAccountDetails', () => {
  it('展示完整基础资料和结构化标签', () => {
    render(
      <AiAccountDetails
        account={createAccount()}
        status="success"
        isEmpty={false}
      />,
    )

    expect(screen.getByRole('heading', { name: '夜影' })).toBeInTheDocument()
    expect(screen.getByText('VC号：Night#01')).toBeInTheDocument()
    expect(screen.getByText('7月23日')).toBeInTheDocument()
    expect(screen.getByText('狮子座')).toBeInTheDocument()
    expect(screen.getByText('中国 上海')).toBeInTheDocument()
    expect(screen.getByText('自由插画师')).toBeInTheDocument()
    expect(screen.getByText('绘画')).toBeInTheDocument()
    expect(screen.getByText('理性')).toBeInTheDocument()
    expect(screen.getByText('在自己的节奏里前进。')).toBeInTheDocument()
  })

  it('允许分别选择头像和封面文件，并显示上传错误', async () => {
    const user = userEvent.setup()
    const uploadAvatar = vi.fn().mockResolvedValue(true)
    const uploadCover = vi.fn().mockResolvedValue(true)
    const { container } = render(
      <AiAccountDetails
        account={{
          ...createAccount(),
          avatarUrl: '/api/ai-accounts/account-1/avatar?v=avatar',
          coverUrl: '/api/ai-accounts/account-1/cover?v=cover',
        }}
        status="success"
        isEmpty={false}
        mediaUploadErrorMessage="图片格式不受支持。"
        onUploadAvatar={uploadAvatar}
        onUploadCover={uploadCover}
      />,
    )
    const avatarFile = new File(['avatar'], 'avatar.png', {
      type: 'image/png',
    })
    const coverFile = new File(['cover'], 'cover.webp', {
      type: 'image/webp',
    })
    const inputs = container.querySelectorAll<HTMLInputElement>(
      'input[type="file"]',
    )

    await user.upload(inputs[0], coverFile)
    await user.upload(inputs[1], avatarFile)

    expect(uploadCover).toHaveBeenCalledWith(coverFile)
    expect(uploadAvatar).toHaveBeenCalledWith(avatarFile)
    expect(screen.getByRole('alert')).toHaveTextContent('图片格式不受支持')
    expect(container.querySelector(`img[src*="/cover?"]`)).toBeInTheDocument()
    expect(container.querySelector(`img[src*="/avatar?"]`)).toBeInTheDocument()
  })
})

function createAccount(): AiAccountResponse {
  return {
    id: 'account-1',
    vcNumber: 'Night#01',
    nickname: '夜影',
    identityDescription: '喜欢安静思考的朋友',
    personality: '冷静、理性',
    speakingStyle: '简洁温和',
    signature: '在自己的节奏里前进。',
    birthday: '2000-07-23',
    age: 26,
    zodiacSign: '狮子座',
    gender: 'Male',
    location: '中国 上海',
    occupation: '自由插画师',
    hometown: '中国 杭州',
    onlineStatus: 'Online',
    avatarUrl: null,
    coverUrl: null,
    interestTags: ['绘画', '阅读'],
    personalityTags: ['冷静', '理性'],
    createdAt: '2026-07-17T12:00:00',
  }
}
