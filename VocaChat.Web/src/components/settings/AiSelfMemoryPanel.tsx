import { useState } from 'react'
import { Archive, Brain, LockKeyhole, Pencil, Plus, RotateCcw } from 'lucide-react'
import type {
  AiSelfMemoryFactNature,
  AiSelfMemoryMutability,
  AiSelfMemoryResponse,
  AiSelfMemoryStatus,
  AiSelfMemoryTrustLevel,
  AiSelfMemoryType,
  CharacterWorldResponse,
  SaveAiSelfMemoryRequest,
} from '@/api/types'
import { AiSelfMemoryEditor } from '@/components/settings/AiSelfMemoryEditor'
import { EmptyState } from '@/components/feedback/EmptyState'
import { ErrorState } from '@/components/feedback/ErrorState'
import { LoadingState } from '@/components/feedback/LoadingState'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useAiSelfMemories } from '@/hooks/useAiSelfMemories'
import { cn } from '@/lib/utils'
import { formatDateTime } from '@/utils/dateTime'

interface AiSelfMemoryPanelProps {
  aiAccountId: string
  characterWorlds?: CharacterWorldResponse[]
  defaultCharacterWorldId?: string
  onDirtyChange: (isDirty: boolean) => void
}

const statusFilters: Array<{ value: AiSelfMemoryStatus; label: string }> = [
  { value: 'Active', label: '有效' },
  { value: 'Superseded', label: '已替代' },
  { value: 'Archived', label: '已归档' },
]

const typeLabels: Record<AiSelfMemoryType, string> = {
  PersonalFact: '个人事实',
  OngoingActivity: '持续事项',
  Plan: '计划',
  Experience: '重要经历',
  Preference: '偏好',
}

const factNatureLabels: Record<AiSelfMemoryFactNature, string> = {
  Objective: '客观',
  Subjective: '主观',
  Narrative: '叙事',
}

const mutabilityLabels: Record<AiSelfMemoryMutability, string> = {
  Immutable: '恒定',
  Mutable: '可变化',
  Evolving: '持续演化',
  Ephemeral: '短期',
}

const trustLevelLabels: Record<AiSelfMemoryTrustLevel, string> = {
  UserCanon: '用户正典',
  EstablishedCanon: '已建立事实',
  NarrativeCandidate: '叙事候选',
  SubjectiveState: '主观状态',
}

/** 查询并管理当前账号的个人记忆，不把它与聊天记录或关系记忆混用。 */
export function AiSelfMemoryPanel({
  aiAccountId,
  characterWorlds = [],
  defaultCharacterWorldId,
  onDirtyChange,
}: AiSelfMemoryPanelProps) {
  const [statusFilter, setStatusFilter] = useState<AiSelfMemoryStatus>('Active')
  const [editorMode, setEditorMode] = useState<'create' | 'edit'>()
  const [editingMemoryId, setEditingMemoryId] = useState<string>()
  const [hasEditorChanges, setHasEditorChanges] = useState(false)
  const memories = useAiSelfMemories(aiAccountId, statusFilter)
  const editingMemory = memories.data.find((memory) => memory.id === editingMemoryId)

  function confirmDiscard(): boolean {
    return !hasEditorChanges || window.confirm('记忆修改尚未保存，确定要放弃吗？')
  }

  function closeEditor() {
    if (!confirmDiscard()) return
    setEditorMode(undefined)
    setEditingMemoryId(undefined)
    setHasEditorChanges(false)
    memories.clearOperationError()
    onDirtyChange(false)
  }

  function changeFilter(nextStatus: AiSelfMemoryStatus) {
    if (nextStatus === statusFilter || !confirmDiscard()) return
    setStatusFilter(nextStatus)
    setEditorMode(undefined)
    setEditingMemoryId(undefined)
    setHasEditorChanges(false)
    memories.clearOperationError()
    onDirtyChange(false)
  }

  function beginEdit(memoryId: string) {
    if (!confirmDiscard()) return
    setEditorMode('edit')
    setEditingMemoryId(memoryId)
    setHasEditorChanges(false)
    memories.clearOperationError()
  }

  async function changeMemoryStatus(memory: AiSelfMemoryResponse) {
    if (!confirmDiscard()) return
    setEditorMode(undefined)
    setEditingMemoryId(undefined)
    setHasEditorChanges(false)
    onDirtyChange(false)
    await memories.changeStatus(
      memory.id,
      memory.status === 'Archived' ? 'Active' : 'Archived',
    )
  }

  function handleEditorDirtyChange(isDirty: boolean) {
    setHasEditorChanges(isDirty)
    onDirtyChange(isDirty)
  }

  async function saveMemory(request: SaveAiSelfMemoryRequest): Promise<boolean> {
    const saved = editorMode === 'edit' && editingMemoryId
      ? await memories.update(editingMemoryId, request)
      : await memories.create(request)
    if (!saved) return false
    setEditorMode(undefined)
    setEditingMemoryId(undefined)
    setHasEditorChanges(false)
    onDirtyChange(false)
    return true
  }

  return (
    <section aria-labelledby="self-memory-title">
      <header className="flex flex-wrap items-center justify-between gap-4 border-b border-border px-5 py-4 sm:px-6">
        <div>
          <h3 id="self-memory-title" className="text-sm font-semibold text-foreground">AI 记忆</h3>
          <p className="mt-1 text-xs leading-5 text-muted-foreground">查看和修正影响该账号身份连续性的长期个人事实。</p>
        </div>
        <Button
          onClick={() => {
            if (!confirmDiscard()) return
            setStatusFilter('Active')
            setEditorMode('create')
            setEditingMemoryId(undefined)
            setHasEditorChanges(false)
            memories.clearOperationError()
          }}
        >
          <Plus className="size-4" aria-hidden="true" />
          新增记忆
        </Button>
      </header>

      <div className="flex gap-1 border-b border-border px-5 pt-3 sm:px-6" role="tablist" aria-label="记忆状态筛选">
        {statusFilters.map((filter) => (
          <button
            key={filter.value}
            type="button"
            role="tab"
            aria-selected={statusFilter === filter.value}
            onClick={() => changeFilter(filter.value)}
            className={cn(
              'relative px-3 py-2 text-sm font-medium text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset',
              statusFilter === filter.value && 'text-primary after:absolute after:inset-x-2 after:bottom-[-1px] after:h-0.5 after:bg-primary',
            )}
          >
            {filter.label}
          </button>
        ))}
      </div>

      {editorMode && (
        <AiSelfMemoryEditor
          memory={editorMode === 'edit' ? editingMemory : undefined}
          characterWorlds={characterWorlds}
          defaultCharacterWorldId={defaultCharacterWorldId}
          isSaving={Boolean(memories.operation)}
          errorMessage={memories.operationErrorMessage}
          onSave={saveMemory}
          onCancel={closeEditor}
          onDirtyChange={handleEditorDirtyChange}
        />
      )}

      {!editorMode && memories.operationErrorMessage && (
        <div className="mx-5 mt-4 rounded-lg border border-destructive/20 bg-danger-soft px-4 py-3 text-sm text-destructive sm:mx-6" role="alert">
          {memories.operationErrorMessage}
        </div>
      )}

      {(memories.status === 'idle' || memories.status === 'loading') && <LoadingState variant="detail" />}
      {memories.status === 'error' && <ErrorState message={memories.errorMessage} onRetry={() => void memories.reload()} />}
      {memories.status === 'success' && memories.data.length === 0 && (
        <EmptyState
          icon={Brain}
          title={`没有${statusFilters.find((item) => item.value === statusFilter)?.label}记忆`}
          description={statusFilter === 'Active' ? '可以新增一条以后仍会影响该账号表达的个人事实。' : '当前筛选范围内没有记录。'}
        />
      )}
      {memories.status === 'success' && memories.data.length > 0 && (
        <ul className="divide-y divide-border" aria-label="个人记忆列表">
          {memories.data.map((memory) => (
            <li key={memory.id} className="px-5 py-5 sm:px-6">
              <div className="flex min-w-0 items-start justify-between gap-4">
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant="secondary">{typeLabels[memory.type]}</Badge>
                    <span className="text-xs text-muted-foreground">
                      {factNatureLabels[memory.factNature]} · {mutabilityLabels[memory.mutability]}
                    </span>
                    <span className="text-xs font-medium text-foreground">
                      {trustLevelLabels[memory.trustLevel]}
                    </span>
                    <span className="text-xs text-muted-foreground">{memory.source === 'User' ? '用户记录' : '导演建议'}</span>
                    {memory.isUserLocked && (
                      <span className="inline-flex items-center gap-1 text-xs font-medium text-primary">
                        <LockKeyhole className="size-3.5" aria-hidden="true" />
                        已锁定
                      </span>
                    )}
                  </div>
                  <p className="mt-3 whitespace-pre-wrap break-words text-sm leading-6 text-foreground">{memory.summary}</p>
                  <p className="mt-3 break-all text-xs leading-5 text-muted-foreground">
                    {getWorldName(memory.characterWorldId, characterWorlds)}
                    {' · '}
                    事实键 <span translate="no">{memory.factKey}</span>
                    {' · '}
                    重要度 {memory.salience}
                    {' · '}
                    更新于 {formatDateTime(memory.updatedAt)}
                  </p>
                  {memory.supersedesMemoryId && (
                    <p className="mt-1 text-xs text-muted-foreground">此版本替代了先前记录。</p>
                  )}
                </div>
                <div className="flex shrink-0 gap-1">
                  {memory.status === 'Active' && (
                    <Button variant="ghost" size="icon" className="size-9" onClick={() => beginEdit(memory.id)} aria-label={`编辑记忆：${memory.summary}`}>
                      <Pencil className="size-4" aria-hidden="true" />
                    </Button>
                  )}
                  {memory.status !== 'Superseded' && (
                    <Button
                      variant="ghost"
                      size="icon"
                      className="size-9"
                      disabled={Boolean(memories.operation)}
                      onClick={() => void changeMemoryStatus(memory)}
                      aria-label={memory.status === 'Archived' ? `恢复记忆：${memory.summary}` : `归档记忆：${memory.summary}`}
                    >
                      {memory.status === 'Archived' ? <RotateCcw className="size-4" aria-hidden="true" /> : <Archive className="size-4" aria-hidden="true" />}
                    </Button>
                  )}
                </div>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function getWorldName(
  characterWorldId: string,
  characterWorlds: CharacterWorldResponse[],
): string {
  return characterWorlds.find((world) => world.id === characterWorldId)?.name
    ?? '未知角色世界'
}
