import { act, renderHook } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { useTokenUsageVisibility } from '@/hooks/useTokenUsageVisibility'

describe('useTokenUsageVisibility', () => {
  beforeEach(() => {
    window.localStorage.clear()
  })

  it('默认隐藏并将用户选择保存到本地设置', () => {
    const { result } = renderHook(() => useTokenUsageVisibility())

    expect(result.current.isVisible).toBe(false)

    act(() => {
      result.current.setIsVisible(true)
    })

    expect(result.current.isVisible).toBe(true)
    expect(
      window.localStorage.getItem('vocachat.show-token-usage'),
    ).toBe('true')
  })
})
