import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { EntityAvatar } from '@/components/common/EntityAvatar'

describe('EntityAvatar', () => {
  it('没有真实图片时显示稳定的昵称首字', () => {
    render(<EntityAvatar name="夜影" />)

    expect(screen.getByText('夜')).toBeInTheDocument()
  })

  it('收到媒体 URL 时渲染真实图片并保留回退内容', () => {
    const { container } = render(
      <EntityAvatar name="夜影" src="/api/ai-accounts/1/avatar?v=image" />,
    )

    expect(container.querySelector('img')).toHaveAttribute(
      'src',
      '/api/ai-accounts/1/avatar?v=image',
    )
    expect(screen.getByText('夜')).toBeInTheDocument()

    fireEvent.error(container.querySelector('img')!)

    expect(container.querySelector('img')).not.toBeInTheDocument()
    expect(screen.getByText('夜')).toBeInTheDocument()
  })
})
