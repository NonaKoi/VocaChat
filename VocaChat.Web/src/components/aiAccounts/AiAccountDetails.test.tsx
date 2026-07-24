import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
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
    expect(screen.getByText('现实世界')).toBeInTheDocument()
    expect(screen.getByText('绘画')).toBeInTheDocument()
    expect(screen.getByText('理性')).toBeInTheDocument()
    expect(screen.getByText('在自己的节奏里前进。')).toBeInTheDocument()
  })

  it('好友主页只展示头像和封面，不提供账号管理入口', () => {
    const { container } = render(
      <AiAccountDetails
        account={{
          ...createAccount(),
          avatarUrl: '/api/ai-accounts/account-1/avatar?v=avatar',
          coverUrl: '/api/ai-accounts/account-1/cover?v=cover',
        }}
        status="success"
        isEmpty={false}
      />,
    )

    expect(container.querySelector('input[type="file"]')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '更换头像' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '更换封面' })).not.toBeInTheDocument()
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
    characterWorldId: 'world-default',
    characterWorld: {
      id: 'world-default',
      name: '现实世界',
      description: '采用现代现实社会的基本规则。',
      createdAt: '2026-07-17T10:00:00Z',
      updatedAt: '2026-07-17T10:00:00Z',
    },
    interestTags: ['绘画', '阅读'],
    personalityTags: ['冷静', '理性'],
    createdAt: '2026-07-17T12:00:00',
  }
}
