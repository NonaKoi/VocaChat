import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  createAiSelfMemory,
  getAiSelfMemories,
  updateAiSelfMemoryStatus,
} from '@/api/aiSelfMemories'
import type { AiSelfMemoryResponse } from '@/api/types'
import { AiSelfMemoryPanel } from '@/components/settings/AiSelfMemoryPanel'

vi.mock('@/api/aiSelfMemories', () => ({
  getAiSelfMemories: vi.fn(),
  createAiSelfMemory: vi.fn(),
  updateAiSelfMemory: vi.fn(),
  updateAiSelfMemoryStatus: vi.fn(),
}))

const getMemoriesMock = vi.mocked(getAiSelfMemories)
const createMemoryMock = vi.mocked(createAiSelfMemory)
const updateStatusMock = vi.mocked(updateAiSelfMemoryStatus)
const accountId = '10000000-0000-0000-0000-000000000001'

describe('AiSelfMemoryPanel', () => {
  beforeEach(() => {
    getMemoriesMock.mockReset()
    createMemoryMock.mockReset()
    updateStatusMock.mockReset()
  })

  it('创建用户记忆并保留结构化字段', async () => {
    const user = userEvent.setup()
    getMemoriesMock.mockResolvedValue([])
    createMemoryMock.mockResolvedValue(createMemory())

    render(<AiSelfMemoryPanel aiAccountId={accountId} onDirtyChange={vi.fn()} />)
    await screen.findByText('没有有效记忆')
    await user.click(screen.getByRole('button', { name: '新增记忆' }))
    await user.type(screen.getByLabelText('记忆摘要'), '最近正在准备城市夜景摄影作品。')
    await user.click(screen.getByRole('button', { name: '保存记忆' }))

    await waitFor(() => expect(createMemoryMock).toHaveBeenCalledWith(
      accountId,
      expect.objectContaining({
        type: 'PersonalFact',
        summary: '最近正在准备城市夜景摄影作品。',
        salience: 60,
        isUserLocked: true,
      }),
    ))
  })

  it('按状态查询并通过状态接口归档记忆', async () => {
    const user = userEvent.setup()
    const memory = createMemory()
    getMemoriesMock.mockResolvedValueOnce([memory]).mockResolvedValue([])
    updateStatusMock.mockResolvedValue({ ...memory, status: 'Archived' })

    render(<AiSelfMemoryPanel aiAccountId={accountId} onDirtyChange={vi.fn()} />)
    await screen.findByText(memory.summary)
    await user.click(screen.getByRole('button', { name: `归档记忆：${memory.summary}` }))

    await waitFor(() => expect(updateStatusMock).toHaveBeenCalledWith(accountId, memory.id, 'Archived'))
    await user.click(screen.getByRole('tab', { name: '已归档' }))
    await waitFor(() => expect(getMemoriesMock).toHaveBeenCalledWith(accountId, 'Archived'))
  })
})

function createMemory(): AiSelfMemoryResponse {
  return {
    id: '20000000-0000-0000-0000-000000000001',
    aiAccountId: accountId,
    type: 'OngoingActivity',
    summary: '正在整理城市夜景摄影作品。',
    source: 'User',
    status: 'Active',
    salience: 70,
    isUserLocked: true,
    sourceConversationId: null,
    sourceMessageId: null,
    occurredAt: null,
    validFrom: null,
    validUntil: null,
    createdAt: '2026-07-20T12:00:00Z',
    updatedAt: '2026-07-20T12:00:00Z',
  }
}
