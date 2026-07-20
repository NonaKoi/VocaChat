import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { LoaderCircle, Save, X } from 'lucide-react'
import type {
  AiSelfMemoryResponse,
  AiSelfMemoryType,
  SaveAiSelfMemoryRequest,
} from '@/api/types'
import { Button } from '@/components/ui/button'

interface AiSelfMemoryEditorProps {
  memory?: AiSelfMemoryResponse
  isSaving: boolean
  errorMessage?: string
  onSave: (request: SaveAiSelfMemoryRequest) => Promise<boolean>
  onCancel: () => void
  onDirtyChange: (isDirty: boolean) => void
}

const memoryTypes: Array<{ value: AiSelfMemoryType; label: string }> = [
  { value: 'PersonalFact', label: '个人事实' },
  { value: 'OngoingActivity', label: '持续事项' },
  { value: 'Plan', label: '计划' },
  { value: 'Experience', label: '重要经历' },
  { value: 'Preference', label: '偏好' },
]

/** 以内联表单编辑个人记忆，避免用模态框打断账号设置流程。 */
export function AiSelfMemoryEditor({
  memory,
  isSaving,
  errorMessage,
  onSave,
  onCancel,
  onDirtyChange,
}: AiSelfMemoryEditorProps) {
  const initialDraft = useMemo(() => toMemoryDraft(memory), [memory])
  const [draft, setDraft] = useState(initialDraft)
  const isDirty = JSON.stringify(draft) !== JSON.stringify(initialDraft)
  const canSave = !isSaving && draft.summary.trim().length > 0 && draft.salience >= 1 && draft.salience <= 100

  useEffect(() => {
    setDraft(initialDraft)
  }, [initialDraft])

  useEffect(() => {
    onDirtyChange(isDirty)
  }, [isDirty, onDirtyChange])

  function updateValue<Key extends keyof SaveAiSelfMemoryRequest>(
    key: Key,
    value: SaveAiSelfMemoryRequest[Key],
  ) {
    setDraft((current) => ({ ...current, [key]: value }))
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canSave) return
    await onSave(draft)
  }

  return (
    <form
      className="border-b border-border bg-surface-muted px-5 py-5 sm:px-6"
      onSubmit={(event) => void handleSubmit(event)}
      aria-labelledby="memory-editor-title"
    >
      <div className="flex items-start justify-between gap-4">
        <div>
          <h4 id="memory-editor-title" className="text-sm font-semibold text-foreground">
            {memory ? '编辑个人记忆' : '新增个人记忆'}
          </h4>
          <p className="mt-1 text-xs leading-5 text-muted-foreground">
            只记录以后仍会影响身份连续性或回复内容的事实。
          </p>
        </div>
        <Button type="button" variant="ghost" size="icon" className="size-8" onClick={onCancel} aria-label="关闭记忆编辑器">
          <X className="size-4" aria-hidden="true" />
        </Button>
      </div>

      {errorMessage && (
        <div className="mt-4 rounded-lg border border-destructive/20 bg-danger-soft px-4 py-3 text-sm text-destructive" role="alert">
          {errorMessage}
        </div>
      )}

      <div className="mt-5 grid gap-5">
        <div className="grid gap-2">
          <label className="text-sm font-semibold text-foreground" htmlFor="memory-summary">记忆摘要</label>
          <textarea
            id="memory-summary"
            name="summary"
            autoComplete="off"
            className="form-control min-h-24 resize-y"
            value={draft.summary}
            maxLength={500}
            required
            autoFocus
            placeholder="例如：最近正在准备一组城市夜景摄影作品…"
            onChange={(event) => updateValue('summary', event.target.value)}
          />
          <span className="justify-self-end text-xs text-muted-foreground">{draft.summary.length}/500</span>
        </div>

        <div className="grid gap-5 xl:grid-cols-2">
          <div className="grid gap-2">
            <label className="text-sm font-semibold text-foreground" htmlFor="memory-type">记忆类型</label>
            <select id="memory-type" name="type" autoComplete="off" className="form-control" value={draft.type} onChange={(event) => updateValue('type', event.target.value as AiSelfMemoryType)}>
              {memoryTypes.map((type) => <option key={type.value} value={type.value}>{type.label}</option>)}
            </select>
          </div>
          <div className="grid gap-2">
            <label className="text-sm font-semibold text-foreground" htmlFor="memory-salience">重要度</label>
            <input
              id="memory-salience"
              name="salience"
              autoComplete="off"
              className="form-control"
              type="number"
              min={1}
              max={100}
              value={draft.salience}
              onChange={(event) => updateValue('salience', Number(event.target.value))}
            />
          </div>
        </div>

        <div className="grid gap-5 xl:grid-cols-3">
          <MemoryDateField id="memory-occurred-at" label="发生时间" value={draft.occurredAt} onChange={(value) => updateValue('occurredAt', value)} />
          <MemoryDateField id="memory-valid-from" label="有效开始" value={draft.validFrom} onChange={(value) => updateValue('validFrom', value)} />
          <MemoryDateField id="memory-valid-until" label="有效结束" value={draft.validUntil} onChange={(value) => updateValue('validUntil', value)} />
        </div>

        <label className="flex min-h-11 items-center justify-between gap-4 rounded-lg border border-border bg-surface px-3 py-2.5">
          <span>
            <span className="block text-sm font-semibold text-foreground">锁定这条记忆</span>
            <span className="mt-0.5 block text-xs text-muted-foreground">锁定后导演不能自动修改或替代。</span>
          </span>
          <input
            type="checkbox"
            name="isUserLocked"
            className="size-4 accent-primary"
            checked={draft.isUserLocked}
            onChange={(event) => updateValue('isUserLocked', event.target.checked)}
          />
        </label>
      </div>

      <div className="mt-5 flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel} disabled={isSaving}>取消</Button>
        <Button type="submit" disabled={!canSave} aria-busy={isSaving}>
          {isSaving ? <LoaderCircle className="size-4 animate-spin" aria-hidden="true" /> : <Save className="size-4" aria-hidden="true" />}
          {isSaving ? '正在保存…' : '保存记忆'}
        </Button>
      </div>
    </form>
  )
}

function MemoryDateField({ id, label, value, onChange }: { id: string; label: string; value: string | null; onChange: (value: string | null) => void }) {
  return (
    <div className="grid gap-2">
      <label className="text-sm font-semibold text-foreground" htmlFor={id}>{label}</label>
      <input id={id} name={id} autoComplete="off" type="datetime-local" className="form-control" value={toDateTimeLocalValue(value)} onChange={(event) => onChange(event.target.value ? new Date(event.target.value).toISOString() : null)} />
    </div>
  )
}

function toMemoryDraft(memory?: AiSelfMemoryResponse): SaveAiSelfMemoryRequest {
  return memory
    ? {
        type: memory.type,
        summary: memory.summary,
        salience: memory.salience,
        isUserLocked: memory.isUserLocked,
        occurredAt: memory.occurredAt,
        validFrom: memory.validFrom,
        validUntil: memory.validUntil,
      }
    : {
        type: 'PersonalFact',
        summary: '',
        salience: 60,
        isUserLocked: true,
        occurredAt: null,
        validFrom: null,
        validUntil: null,
      }
}

function toDateTimeLocalValue(value: string | null): string {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  const offset = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offset).toISOString().slice(0, 16)
}
