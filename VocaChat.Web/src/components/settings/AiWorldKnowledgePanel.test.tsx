import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type {
  AiWorldAwarenessOverviewResponse,
  AiWorldKnowledgeEvidenceResponse,
  AiWorldKnowledgeResponse,
} from '@/api/types'
import {
  getAiWorldAwareness,
  getAiWorldKnowledge,
  getAiWorldKnowledgeEvidence,
  updateAiWorldKnowledge,
  updateParallelWorldAwareness,
} from '@/api/aiWorldKnowledge'
import { AiWorldKnowledgePanel } from '@/components/settings/AiWorldKnowledgePanel'

vi.mock('@/api/aiWorldKnowledge', () => ({
  getAiWorldAwareness: vi.fn(),
  getAiWorldKnowledge: vi.fn(),
  getAiWorldKnowledgeEvidence: vi.fn(),
  updateAiWorldKnowledge: vi.fn(),
  updateAiWorldKnowledgeLock: vi.fn(),
  archiveAiWorldKnowledge: vi.fn(),
  updateParallelWorldAwareness: vi.fn(),
  updateSubjectWorldAwareness: vi.fn(),
}))

const accountId = 'account-owner'
const subjectId = 'account-subject'
const knowledge = createKnowledge()
const evidence = createEvidence()

describe('AiWorldKnowledgePanel', () => {
  beforeEach(() => {
    vi.mocked(getAiWorldAwareness).mockReset().mockResolvedValue(
      createOverview(),
    )
    vi.mocked(getAiWorldKnowledge).mockReset().mockResolvedValue([knowledge])
    vi.mocked(getAiWorldKnowledgeEvidence).mockReset().mockResolvedValue(
      [evidence],
    )
    vi.mocked(updateAiWorldKnowledge).mockReset().mockResolvedValue({
      ...knowledge,
      trustLevel: 'UserConfirmed',
      isUserLocked: true,
    })
    vi.mocked(updateParallelWorldAwareness).mockReset().mockResolvedValue({
      state: 'Accepted',
      isUserLocked: false,
      firstInformedAt: '2026-07-24T10:00:00Z',
      acceptedAt: '2026-07-24T10:00:00Z',
      updatedAt: '2026-07-24T10:00:00Z',
    })
  })

  it('展示派生熟悉度、世界知识和真实消息来源', async () => {
    render(
      <AiWorldKnowledgePanel
        aiAccountId={accountId}
        onDirtyChange={vi.fn()}
      />,
    )

    expect(await screen.findByText('初步印象')).toBeInTheDocument()
    expect(await screen.findByDisplayValue(knowledge.summary)).toBeInTheDocument()
    expect(await screen.findByText(evidence.messageContent)).toBeInTheDocument()
    expect(screen.getByText('私信')).toBeInTheDocument()
    expect(screen.queryByText(knowledge.knowledgeKey)).not.toBeInTheDocument()
  })

  it('允许修改确认知识并管理平行世界认知', async () => {
    const user = userEvent.setup()
    const dirtyChange = vi.fn()
    render(
      <AiWorldKnowledgePanel
        aiAccountId={accountId}
        onDirtyChange={dirtyChange}
      />,
    )

    const summary = await screen.findByLabelText('认知摘要')
    await user.clear(summary)
    await user.type(summary, '用户修正后的世界知识。')
    await user.click(screen.getByLabelText('锁定，阻止自动流程修改'))
    await user.click(screen.getByRole('button', { name: '保存并确认' }))

    await waitFor(() => expect(updateAiWorldKnowledge).toHaveBeenCalledWith(
      accountId,
      knowledge.id,
      expect.objectContaining({
        summary: '用户修正后的世界知识。',
        isUserLocked: true,
      }),
    ))
    expect(dirtyChange).toHaveBeenCalledWith(true)

    await user.selectOptions(
      screen.getByLabelText('平行世界认知状态'),
      'Accepted',
    )
    await waitFor(() => expect(updateParallelWorldAwareness).toHaveBeenCalledWith(
      accountId,
      'Accepted',
      false,
    ))
  })
})

function createOverview(): AiWorldAwarenessOverviewResponse {
  return {
    aiAccountId: accountId,
    parallelWorld: {
      state: 'Informed',
      isUserLocked: false,
      firstInformedAt: '2026-07-24T09:00:00Z',
      acceptedAt: null,
      updatedAt: '2026-07-24T09:00:00Z',
    },
    subjects: [
      {
        aiAccountId: subjectId,
        nickname: '异世界讲述者',
        avatarUrl: null,
        characterWorldId: 'world-other',
        characterWorldName: '沙漠学园世界',
        awarenessState: 'DifferentBackgroundRecognized',
        isUserLocked: false,
        awarenessEvidenceCount: 1,
        awarenessConversationCount: 1,
        familiarityLevel: 'FirstImpression',
        activeKnowledgeCount: 1,
        distinctTopicCount: 1,
        knowledgeEvidenceCount: 1,
        knowledgeConversationCount: 1,
      },
    ],
  }
}

function createKnowledge(): AiWorldKnowledgeResponse {
  return {
    id: 'knowledge-1',
    ownerAiAccountId: accountId,
    subjectCharacterWorldId: 'world-other',
    subjectAiAccountId: subjectId,
    knowledgeKey: 'school.desertification',
    summary: '讲述者所在世界的学校受到沙漠化影响。',
    factNature: 'ObjectiveStatement',
    mutability: 'Constant',
    trustLevel: 'DirectStatement',
    status: 'Active',
    salience: 80,
    isUserLocked: false,
    evidenceCount: 1,
    firstLearnedAt: '2026-07-24T09:00:00Z',
    updatedAt: '2026-07-24T09:00:00Z',
  }
}

function createEvidence(): AiWorldKnowledgeEvidenceResponse {
  return {
    evidenceId: 'evidence-1',
    sourceType: 'AiAccount',
    sourceAiAccountId: subjectId,
    sourceDisplayName: '异世界讲述者',
    conversationKind: 'PrivateChat',
    conversationId: 'private-chat-1',
    conversationDisplayName: '私信',
    messageId: 'message-1',
    messageContent: '我们学校这些年一直受到沙漠化影响。',
    sentAt: '2026-07-24T09:00:00Z',
    evidenceSummary: '当事人在私信中直接说明学校面临沙漠化。',
  }
}
