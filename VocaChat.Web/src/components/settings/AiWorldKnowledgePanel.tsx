import { useEffect, useMemo, useState } from 'react'
import {
  Archive,
  BookOpenText,
  CircleHelp,
  Globe2,
  LockKeyhole,
  MessageSquareText,
  ShieldCheck,
} from 'lucide-react'
import type {
  AiParallelWorldAwarenessState,
  AiWorldAwarenessState,
  AiWorldKnowledgeEvidenceResponse,
  AiWorldKnowledgeFactNature,
  AiWorldKnowledgeMutability,
  AiWorldKnowledgeResponse,
  AiWorldKnowledgeStatus,
  UpdateAiWorldKnowledgeRequest,
  WorldAwarenessSubjectResponse,
} from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { EmptyState } from '@/components/feedback/EmptyState'
import { ErrorState } from '@/components/feedback/ErrorState'
import { LoadingState } from '@/components/feedback/LoadingState'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useAiWorldKnowledgeManagement } from '@/hooks/useAiWorldKnowledgeManagement'
import { cn } from '@/lib/utils'
import type { RemoteStatus } from '@/types/remoteStatus'
import { formatDateTime } from '@/utils/dateTime'

interface AiWorldKnowledgePanelProps {
  aiAccountId: string
  onDirtyChange: (isDirty: boolean) => void
}

type KnowledgeDraft = UpdateAiWorldKnowledgeRequest

const knowledgeFilters: Array<{
  value: AiWorldKnowledgeStatus
  label: string
}> = [
  { value: 'Active', label: '当前认知' },
  { value: 'ConflictCandidate', label: '待确认冲突' },
  { value: 'Superseded', label: '历史版本' },
  { value: 'Archived', label: '已归档' },
]

const parallelStateLabels: Record<AiParallelWorldAwarenessState, string> = {
  Unaware: '尚未知晓',
  Informed: '已经获知',
  Accepted: '已经接受',
}

const awarenessStateLabels: Record<AiWorldAwarenessState, string> = {
  AssumedSharedWorld: '默认同一世界',
  AnomalyObserved: '察觉到差异',
  DifferentBackgroundRecognized: '确认背景不同',
  CrossWorldConfirmed: '确认来自其他世界',
}

const familiarityLabels = {
  Unfamiliar: '尚不熟悉',
  FirstImpression: '初步印象',
  Learning: '了解中',
  Familiar: '较为熟悉',
} as const

const factNatureLabels: Record<AiWorldKnowledgeFactNature, string> = {
  ObjectiveStatement: '客观陈述',
  SubjectiveView: '主观看法',
  Hearsay: '转述信息',
  Unconfirmed: '尚未确认',
}

const mutabilityLabels: Record<AiWorldKnowledgeMutability, string> = {
  Constant: '恒定事实',
  Changeable: '可能变化',
  Temporary: '短期信息',
}

const trustLabels = {
  Unverified: '未验证',
  DirectStatement: '当事人陈述',
  Corroborated: '多来源印证',
  UserConfirmed: '用户确认',
} as const

/** 管理一个账号对其他角色世界的认知、知识版本和真实消息来源。 */
export function AiWorldKnowledgePanel({
  aiAccountId,
  onDirtyChange,
}: AiWorldKnowledgePanelProps) {
  const [statusFilter, setStatusFilter] =
    useState<AiWorldKnowledgeStatus>('Active')
  const [selectedSubjectId, setSelectedSubjectId] = useState<string>()
  const [selectedKnowledgeId, setSelectedKnowledgeId] = useState<string>()
  const [draft, setDraft] = useState<KnowledgeDraft>()
  const [evidence, setEvidence] =
    useState<AiWorldKnowledgeEvidenceResponse[]>([])
  const [evidenceStatus, setEvidenceStatus] =
    useState<RemoteStatus>('idle')
  const [evidenceError, setEvidenceError] = useState<string>()
  const management = useAiWorldKnowledgeManagement(
    aiAccountId,
    selectedSubjectId,
    statusFilter,
  )
  const { loadEvidence } = management
  const selectedSubject = management.overview?.subjects.find(
    (subject) => subject.aiAccountId === selectedSubjectId,
  )
  const selectedKnowledge = management.knowledge.find(
    (item) => item.id === selectedKnowledgeId,
  )
  const originalDraft = useMemo(
    () => selectedKnowledge ? toDraft(selectedKnowledge) : undefined,
    [selectedKnowledge],
  )
  const isDirty = Boolean(
    draft
    && originalDraft
    && JSON.stringify(draft) !== JSON.stringify(originalDraft),
  )

  useEffect(() => {
    if (!selectedSubjectId && management.overview?.subjects[0]) {
      setSelectedSubjectId(management.overview.subjects[0].aiAccountId)
    }
  }, [management.overview, selectedSubjectId])

  useEffect(() => {
    if (
      selectedKnowledgeId
      && management.knowledge.some((item) => item.id === selectedKnowledgeId)
    ) return
    setSelectedKnowledgeId(management.knowledge[0]?.id)
  }, [management.knowledge, selectedKnowledgeId])

  useEffect(() => {
    setDraft(originalDraft)
  }, [originalDraft])

  useEffect(() => {
    onDirtyChange(isDirty)
  }, [isDirty, onDirtyChange])

  useEffect(() => {
    if (!selectedKnowledgeId) {
      setEvidence([])
      setEvidenceStatus('idle')
      return
    }

    let isCurrent = true
    setEvidenceStatus('loading')
    setEvidenceError(undefined)
    void loadEvidence(selectedKnowledgeId)
      .then((sources) => {
        if (!isCurrent) return
        setEvidence(sources)
        setEvidenceStatus('success')
      })
      .catch((error: unknown) => {
        if (!isCurrent) return
        setEvidence([])
        setEvidenceStatus('error')
        setEvidenceError(toMessage(error, '知识来源加载失败，请重试。'))
      })
    return () => {
      isCurrent = false
    }
  }, [loadEvidence, selectedKnowledgeId])

  function confirmDiscard(): boolean {
    return !isDirty || window.confirm('当前世界知识修改尚未保存，确定要放弃吗？')
  }

  function selectSubject(subjectId: string) {
    if (subjectId === selectedSubjectId || !confirmDiscard()) return
    setSelectedSubjectId(subjectId)
    setSelectedKnowledgeId(undefined)
    management.clearOperationError()
  }

  function changeStatusFilter(status: AiWorldKnowledgeStatus) {
    if (status === statusFilter || !confirmDiscard()) return
    setStatusFilter(status)
    setSelectedKnowledgeId(undefined)
    management.clearOperationError()
  }

  async function saveKnowledge() {
    if (!selectedKnowledge || !draft) return
    const saved = await management.updateKnowledge(
      selectedKnowledge.id,
      draft,
    )
    if (saved) onDirtyChange(false)
  }

  async function archiveKnowledge() {
    if (!selectedKnowledge) return
    if (!window.confirm('归档后仍会保留内容和全部消息来源，确定继续吗？')) {
      return
    }
    await management.archive(selectedKnowledge.id)
    setSelectedKnowledgeId(undefined)
  }

  return (
    <section aria-labelledby="world-knowledge-title">
      <header className="border-b border-border px-5 py-4 sm:px-6">
        <h3 id="world-knowledge-title" className="text-sm font-semibold text-foreground">
          世界认知
        </h3>
        <p className="mt-1 max-w-3xl text-xs leading-5 text-muted-foreground">
          查看这个账号如何理解其他角色和世界，并修正确认、冲突或过时的知识。所有内容都保留真实会话来源。
        </p>
      </header>

      {(management.overviewStatus === 'idle'
        || management.overviewStatus === 'loading') && (
        <LoadingState variant="detail" />
      )}
      {management.overviewStatus === 'error' && (
        <ErrorState
          message={management.overviewError}
          onRetry={() => void management.reloadOverview()}
        />
      )}
      {management.overviewStatus === 'success' && management.overview && (
        <>
          <div className="grid border-b border-border bg-surface-muted/45 xl:grid-cols-[minmax(260px,0.8fr)_minmax(0,1.2fr)]">
            <ParallelAwarenessEditor
              state={management.overview.parallelWorld.state}
              isUserLocked={management.overview.parallelWorld.isUserLocked}
              disabled={Boolean(management.operation)}
              onChange={(state, locked) => {
                void management.updateParallel(state, locked)
              }}
            />
            <div className="border-t border-border px-5 py-4 xl:border-t-0 xl:border-l sm:px-6">
              <label className="text-xs font-semibold text-foreground" htmlFor="world-knowledge-subject">
                正在了解的好友
              </label>
              {management.overview.subjects.length === 0 ? (
                <p className="mt-2 text-sm text-muted-foreground">
                  暂无其他好友可供建立方向性世界认知。
                </p>
              ) : (
                <div className="mt-2 flex min-w-0 items-center gap-3">
                  <select
                    id="world-knowledge-subject"
                    name="worldKnowledgeSubject"
                    autoComplete="off"
                    className="form-control min-w-0 flex-1"
                    value={selectedSubjectId ?? ''}
                    onChange={(event) => selectSubject(event.target.value)}
                  >
                    {management.overview.subjects.map((subject) => (
                      <option key={subject.aiAccountId} value={subject.aiAccountId}>
                        {subject.nickname} · {subject.characterWorldName}
                      </option>
                    ))}
                  </select>
                  {selectedSubject && (
                    <Badge>
                      {familiarityLabels[selectedSubject.familiarityLevel]}
                    </Badge>
                  )}
                </div>
              )}
              {selectedSubject && (
                <SubjectAwarenessEditor
                  subject={selectedSubject}
                  disabled={Boolean(management.operation)}
                  onChange={(state, locked) => {
                    void management.updateSubject(
                      selectedSubject.aiAccountId,
                      state,
                      locked,
                    )
                  }}
                />
              )}
            </div>
          </div>

          {management.operationError && (
            <div className="mx-5 mt-4 rounded-lg border border-destructive/20 bg-danger-soft px-4 py-3 text-sm text-destructive sm:mx-6" role="alert">
              {management.operationError}
            </div>
          )}

          <div className="flex gap-1 overflow-x-auto border-b border-border px-5 pt-3 sm:px-6" role="tablist" aria-label="世界知识状态">
            {knowledgeFilters.map((filter) => (
              <button
                key={filter.value}
                type="button"
                role="tab"
                aria-selected={statusFilter === filter.value}
                onClick={() => changeStatusFilter(filter.value)}
                className={cn(
                  'relative shrink-0 px-3 py-2 text-sm font-medium text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset',
                  statusFilter === filter.value
                    && 'text-primary after:absolute after:inset-x-2 after:bottom-[-1px] after:h-0.5 after:bg-primary',
                )}
              >
                {filter.label}
              </button>
            ))}
          </div>

          <div className="grid min-h-[420px] xl:grid-cols-[minmax(240px,0.78fr)_minmax(360px,1.22fr)]">
            <KnowledgeList
              items={management.knowledge}
              status={management.knowledgeStatus}
              errorMessage={management.knowledgeError}
              selectedId={selectedKnowledgeId}
              onSelect={(id) => {
                if (id === selectedKnowledgeId || !confirmDiscard()) return
                setSelectedKnowledgeId(id)
                management.clearOperationError()
              }}
              onRetry={() => void management.reloadKnowledge()}
            />
            <KnowledgeDetails
              knowledge={selectedKnowledge}
              draft={draft}
              evidence={evidence}
              evidenceStatus={evidenceStatus}
              evidenceError={evidenceError}
              readOnly={
                statusFilter === 'Superseded'
                || statusFilter === 'Archived'
              }
              isSaving={Boolean(management.operation)}
              onDraftChange={setDraft}
              onSave={() => void saveKnowledge()}
              onArchive={() => void archiveKnowledge()}
              onRetryEvidence={() => {
                if (!selectedKnowledgeId) return
                setEvidenceStatus('loading')
                void management.loadEvidence(selectedKnowledgeId)
                  .then((sources) => {
                    setEvidence(sources)
                    setEvidenceStatus('success')
                  })
                  .catch((error: unknown) => {
                    setEvidenceStatus('error')
                    setEvidenceError(toMessage(
                      error,
                      '知识来源加载失败，请重试。',
                    ))
                  })
              }}
            />
          </div>
        </>
      )}
    </section>
  )
}

function ParallelAwarenessEditor({
  state,
  isUserLocked,
  disabled,
  onChange,
}: {
  state: AiParallelWorldAwarenessState
  isUserLocked: boolean
  disabled: boolean
  onChange: (state: AiParallelWorldAwarenessState, locked: boolean) => void
}) {
  return (
    <div className="px-5 py-4 sm:px-6">
      <div className="flex items-center gap-2">
        <Globe2 className="size-4 text-primary" aria-hidden="true" />
        <h4 className="text-xs font-semibold text-foreground">平行世界认知</h4>
      </div>
      <div className="mt-3 grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center">
        <label>
          <span className="sr-only">平行世界认知状态</span>
          <select
            name="parallelWorldAwareness"
            autoComplete="off"
            className="form-control"
            value={state}
            disabled={disabled}
            onChange={(event) => onChange(
              event.target.value as AiParallelWorldAwarenessState,
              isUserLocked,
            )}
          >
            {Object.entries(parallelStateLabels).map(([value, label]) => (
              <option key={value} value={value}>{label}</option>
            ))}
          </select>
        </label>
        <Checkbox
          name="parallelWorldLocked"
          label="由用户锁定"
          checked={isUserLocked}
          disabled={disabled}
          onChange={(checked) => onChange(state, checked)}
        />
      </div>
    </div>
  )
}

function SubjectAwarenessEditor({
  subject,
  disabled,
  onChange,
}: {
  subject: WorldAwarenessSubjectResponse
  disabled: boolean
  onChange: (state: AiWorldAwarenessState, locked: boolean) => void
}) {
  return (
    <div className="mt-3 flex flex-wrap items-center gap-3">
      <EntityAvatar
        name={subject.nickname}
        src={subject.avatarUrl}
        size="small"
      />
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium text-foreground">
          {subject.nickname}
        </p>
        <p className="mt-0.5 text-xs text-muted-foreground">
          {subject.distinctTopicCount} 个主题 · {subject.knowledgeConversationCount} 段会话
        </p>
      </div>
      <select
        name="subjectWorldAwareness"
        autoComplete="off"
        className="form-control w-auto min-w-44"
        aria-label={`${subject.nickname}的世界认知状态`}
        value={subject.awarenessState}
        disabled={disabled}
        onChange={(event) => onChange(
          event.target.value as AiWorldAwarenessState,
          subject.isUserLocked,
        )}
      >
        {Object.entries(awarenessStateLabels).map(([value, label]) => (
          <option key={value} value={value}>{label}</option>
        ))}
      </select>
      <Checkbox
        name="subjectAwarenessLocked"
        label="锁定"
        checked={subject.isUserLocked}
        disabled={disabled}
        onChange={(checked) => onChange(subject.awarenessState, checked)}
      />
    </div>
  )
}

function KnowledgeList({
  items,
  status,
  errorMessage,
  selectedId,
  onSelect,
  onRetry,
}: {
  items: AiWorldKnowledgeResponse[]
  status: RemoteStatus
  errorMessage?: string
  selectedId?: string
  onSelect: (id: string) => void
  onRetry: () => void
}) {
  return (
    <div className="min-h-0 border-b border-border xl:border-r xl:border-b-0">
      {(status === 'idle' || status === 'loading') && (
        <LoadingState variant="list" />
      )}
      {status === 'error' && (
        <ErrorState message={errorMessage} onRetry={onRetry} />
      )}
      {status === 'success' && items.length === 0 && (
        <EmptyState
          icon={BookOpenText}
          title="当前范围没有世界知识"
          description="知识只会从该账号实际看到的私信或群聊消息中形成。"
          compact
        />
      )}
      {status === 'success' && items.length > 0 && (
        <ul className="divide-y divide-border" aria-label="世界知识列表">
          {items.map((item) => (
            <li key={item.id}>
              <button
                type="button"
                aria-pressed={selectedId === item.id}
                onClick={() => onSelect(item.id)}
                className={cn(
                  'w-full px-5 py-4 text-left outline-none transition-colors hover:bg-surface-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset',
                  selectedId === item.id && 'bg-primary-soft',
                )}
              >
                <div className="flex items-center gap-2">
                  <Badge variant="secondary">
                    {trustLabels[item.trustLevel]}
                  </Badge>
                  {item.isUserLocked && (
                    <LockKeyhole
                      className="size-3.5 text-primary"
                      aria-label="已锁定"
                    />
                  )}
                  <span className="ml-auto text-xs text-muted-foreground">
                    {item.evidenceCount} 个来源
                  </span>
                </div>
                <p className="mt-2 line-clamp-3 text-sm leading-6 text-foreground">
                  {item.summary}
                </p>
                <p className="mt-2 text-xs text-muted-foreground">
                  {factNatureLabels[item.factNature]} · {formatDateTime(item.updatedAt)}
                </p>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function KnowledgeDetails({
  knowledge,
  draft,
  evidence,
  evidenceStatus,
  evidenceError,
  readOnly,
  isSaving,
  onDraftChange,
  onSave,
  onArchive,
  onRetryEvidence,
}: {
  knowledge?: AiWorldKnowledgeResponse
  draft?: KnowledgeDraft
  evidence: AiWorldKnowledgeEvidenceResponse[]
  evidenceStatus: RemoteStatus
  evidenceError?: string
  readOnly: boolean
  isSaving: boolean
  onDraftChange: (draft: KnowledgeDraft) => void
  onSave: () => void
  onArchive: () => void
  onRetryEvidence: () => void
}) {
  if (!knowledge || !draft) {
    return (
      <EmptyState
        icon={CircleHelp}
        title="选择一条知识"
        description="查看内容、可信度、版本状态和形成这条认识的真实消息。"
      />
    )
  }

  const updateDraft = <K extends keyof KnowledgeDraft>(
    key: K,
    value: KnowledgeDraft[K],
  ) => onDraftChange({ ...draft, [key]: value })

  return (
    <div className="min-w-0">
      <div className="border-b border-border px-5 py-5 sm:px-6">
        <div className="flex flex-wrap items-center gap-2">
          <Badge>{trustLabels[knowledge.trustLevel]}</Badge>
          <span className="text-xs text-muted-foreground">
            {factNatureLabels[knowledge.factNature]} · {mutabilityLabels[knowledge.mutability]}
          </span>
          {knowledge.status === 'ConflictCandidate' && (
            <span className="text-xs font-semibold text-destructive">
              与当前恒定事实冲突
            </span>
          )}
        </div>

        <label className="mt-4 block text-sm font-semibold text-foreground" htmlFor="world-knowledge-summary">
          认知摘要
        </label>
        <textarea
          id="world-knowledge-summary"
          name="worldKnowledgeSummary"
          autoComplete="off"
          className="form-control mt-2 min-h-28 resize-y"
          value={draft.summary}
          maxLength={1000}
          readOnly={readOnly}
          onChange={(event) => updateDraft('summary', event.target.value)}
        />

        <div className="mt-4 grid gap-4 sm:grid-cols-3">
          <label className="text-sm font-semibold text-foreground">
            事实性质
            <select
              name="worldKnowledgeFactNature"
              autoComplete="off"
              className="form-control mt-2"
              value={draft.factNature}
              disabled={readOnly}
              onChange={(event) => updateDraft(
                'factNature',
                event.target.value as AiWorldKnowledgeFactNature,
              )}
            >
              {Object.entries(factNatureLabels).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
          </label>
          <label className="text-sm font-semibold text-foreground">
            可变性
            <select
              name="worldKnowledgeMutability"
              autoComplete="off"
              className="form-control mt-2"
              value={draft.mutability}
              disabled={readOnly}
              onChange={(event) => updateDraft(
                'mutability',
                event.target.value as AiWorldKnowledgeMutability,
              )}
            >
              {Object.entries(mutabilityLabels).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
          </label>
          <label className="text-sm font-semibold text-foreground">
            重要度
            <input
              type="number"
              name="worldKnowledgeSalience"
              min={1}
              max={100}
              className="form-control mt-2"
              value={draft.salience}
              readOnly={readOnly}
              onChange={(event) => updateDraft(
                'salience',
                Number(event.target.value),
              )}
            />
          </label>
        </div>

        {!readOnly && (
          <div className="mt-4 flex flex-wrap items-center gap-x-5 gap-y-3">
            <Checkbox
              name="worldKnowledgeLocked"
              label="锁定，阻止自动流程修改"
              checked={draft.isUserLocked}
              disabled={isSaving}
              onChange={(checked) => updateDraft('isUserLocked', checked)}
            />
            {knowledge.status === 'ConflictCandidate' && (
              <Checkbox
                name="worldKnowledgeConfirmed"
                label="确认此候选并替代当前版本"
                checked={draft.isConfirmed}
                disabled={isSaving}
                onChange={(checked) => updateDraft('isConfirmed', checked)}
              />
            )}
            <div className="ml-auto flex gap-2">
              <Button
                variant="outline"
                disabled={isSaving}
                onClick={onArchive}
              >
                <Archive className="size-4" aria-hidden="true" />
                归档
              </Button>
              <Button
                disabled={isSaving || !draft.summary.trim()}
                onClick={onSave}
              >
                <ShieldCheck className="size-4" aria-hidden="true" />
                {isSaving ? '保存中…' : '保存并确认'}
              </Button>
            </div>
          </div>
        )}
      </div>

      <EvidenceList
        evidence={evidence}
        status={evidenceStatus}
        errorMessage={evidenceError}
        onRetry={onRetryEvidence}
      />
    </div>
  )
}

function EvidenceList({
  evidence,
  status,
  errorMessage,
  onRetry,
}: {
  evidence: AiWorldKnowledgeEvidenceResponse[]
  status: RemoteStatus
  errorMessage?: string
  onRetry: () => void
}) {
  return (
    <div className="px-5 py-5 sm:px-6">
      <div className="flex items-center gap-2">
        <MessageSquareText className="size-4 text-primary" aria-hidden="true" />
        <h4 className="text-sm font-semibold text-foreground">消息来源</h4>
        <span className="text-xs text-muted-foreground">
          共 {evidence.length} 条
        </span>
      </div>
      {(status === 'idle' || status === 'loading') && (
        <LoadingState variant="list" />
      )}
      {status === 'error' && (
        <ErrorState message={errorMessage} onRetry={onRetry} />
      )}
      {status === 'success' && evidence.length === 0 && (
        <p className="mt-4 text-sm text-muted-foreground">
          这条知识暂时没有可读取的来源消息。
        </p>
      )}
      {status === 'success' && evidence.length > 0 && (
        <ol className="mt-4 grid gap-3">
          {evidence.map((source) => (
            <li
              key={source.evidenceId}
              className="border-l-2 border-primary/35 pl-4"
            >
              <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                <span className="font-medium text-foreground">
                  {source.sourceDisplayName}
                </span>
                <span>
                  {source.conversationKind === 'PrivateChat'
                    ? '私信'
                    : source.conversationDisplayName}
                </span>
                <time dateTime={source.sentAt}>
                  {formatDateTime(source.sentAt)}
                </time>
              </div>
              <blockquote className="mt-2 whitespace-pre-wrap break-words text-sm leading-6 text-foreground">
                {source.messageContent}
              </blockquote>
              <p className="mt-1 text-xs leading-5 text-muted-foreground">
                {source.evidenceSummary}
              </p>
            </li>
          ))}
        </ol>
      )}
    </div>
  )
}

function Checkbox({
  name,
  label,
  checked,
  disabled,
  onChange,
}: {
  name: string
  label: string
  checked: boolean
  disabled: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <label className="inline-flex cursor-pointer items-center gap-2 text-xs font-medium text-foreground">
      <input
        type="checkbox"
        name={name}
        className="size-4 accent-primary"
        checked={checked}
        disabled={disabled}
        onChange={(event) => onChange(event.target.checked)}
      />
      {label}
    </label>
  )
}

function toDraft(knowledge: AiWorldKnowledgeResponse): KnowledgeDraft {
  return {
    summary: knowledge.summary,
    factNature: knowledge.factNature,
    mutability: knowledge.mutability,
    salience: knowledge.salience,
    isUserLocked: knowledge.isUserLocked,
    isConfirmed: knowledge.status !== 'ConflictCandidate',
  }
}

function toMessage(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback
}
