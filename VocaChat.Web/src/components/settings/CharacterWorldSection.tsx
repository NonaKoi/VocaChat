import { useEffect, useMemo, useState } from 'react'
import {
  Globe2,
  LoaderCircle,
  Pencil,
  Plus,
  Save,
  UsersRound,
  X,
} from 'lucide-react'
import type {
  CharacterWorldResponse,
  CreateCharacterWorldRequest,
  UpdateCharacterWorldRequest,
} from '@/api/types'
import { Button } from '@/components/ui/button'
import type { RemoteStatus } from '@/types/remoteStatus'

type WorldEditorMode = 'closed' | 'create' | 'edit'

interface CharacterWorldSectionProps {
  currentWorld: CharacterWorldResponse
  selectedWorldId: string
  worlds: CharacterWorldResponse[]
  worldUsageCounts: ReadonlyMap<string, number>
  status: RemoteStatus
  errorMessage?: string
  mutationErrorMessage?: string
  isCreating: boolean
  updatingWorldId?: string
  onSelectWorld: (worldId: string) => void
  onReload: () => void
  onCreate: (
    request: CreateCharacterWorldRequest,
  ) => Promise<CharacterWorldResponse | undefined>
  onUpdate: (
    worldId: string,
    request: UpdateCharacterWorldRequest,
  ) => Promise<CharacterWorldResponse | undefined>
  onWorldChanged: () => void | Promise<void>
  onClearMutationError: () => void
  onDirtyChange: (isDirty: boolean) => void
}

interface WorldDraft {
  name: string
  description: string
}

const emptyDraft: WorldDraft = { name: '', description: '' }

/** 在账号资料表单中选择、创建或修改共享角色世界。 */
export function CharacterWorldSection({
  currentWorld,
  selectedWorldId,
  worlds,
  worldUsageCounts,
  status,
  errorMessage,
  mutationErrorMessage,
  isCreating,
  updatingWorldId,
  onSelectWorld,
  onReload,
  onCreate,
  onUpdate,
  onWorldChanged,
  onClearMutationError,
  onDirtyChange,
}: CharacterWorldSectionProps) {
  const [mode, setMode] = useState<WorldEditorMode>('closed')
  const [baseline, setBaseline] = useState<WorldDraft>(emptyDraft)
  const [draft, setDraft] = useState<WorldDraft>(emptyDraft)
  const selectedWorld = worlds.find(
    (world) => world.id === selectedWorldId,
  ) ?? currentWorld
  const isBusy = isCreating || updatingWorldId !== undefined
  const isEditorDirty = mode !== 'closed'
    && (draft.name !== baseline.name
      || draft.description !== baseline.description)
  const canSaveWorld = isEditorDirty
    && draft.name.trim().length > 0
    && !isBusy
  const usageCount = worldUsageCounts.get(selectedWorld.id) ?? 0
  const sortedWorlds = useMemo(() => {
    const availableWorlds = worlds.some(
      (world) => world.id === currentWorld.id,
    )
      ? [...worlds]
      : [currentWorld, ...worlds]

    return availableWorlds.sort((first, second) => (
      first.createdAt.localeCompare(second.createdAt)
      || first.id.localeCompare(second.id)
    ))
  }, [currentWorld, worlds])

  useEffect(() => {
    onDirtyChange(isEditorDirty)
  }, [isEditorDirty, onDirtyChange])

  function openCreate() {
    onClearMutationError()
    setMode('create')
    setBaseline(emptyDraft)
    setDraft(emptyDraft)
  }

  function openEdit() {
    const nextDraft = {
      name: selectedWorld.name,
      description: selectedWorld.description,
    }
    onClearMutationError()
    setMode('edit')
    setBaseline(nextDraft)
    setDraft(nextDraft)
  }

  function closeEditor() {
    onClearMutationError()
    setMode('closed')
    setBaseline(emptyDraft)
    setDraft(emptyDraft)
  }

  async function saveWorld() {
    if (!canSaveWorld) return

    const request = {
      name: draft.name,
      description: draft.description,
    }
    const savedWorld = mode === 'create'
      ? await onCreate(request)
      : await onUpdate(selectedWorld.id, request as UpdateCharacterWorldRequest)
    if (!savedWorld) return

    if (mode === 'create') onSelectWorld(savedWorld.id)
    await onWorldChanged()
    closeEditor()
  }

  return (
    <div className="grid gap-5">
      <div className="grid items-end gap-4 xl:grid-cols-[minmax(0,1fr)_auto]">
        <label className="grid gap-2 text-sm font-semibold text-foreground">
          当前角色世界
          <select
            name="characterWorldId"
            autoComplete="off"
            className="form-control"
            value={selectedWorldId}
            disabled={status !== 'success' || isBusy || mode !== 'closed'}
            onChange={(event) => onSelectWorld(event.target.value)}
          >
            {sortedWorlds.map((world) => (
              <option key={world.id} value={world.id}>
                {world.name}
              </option>
            ))}
          </select>
        </label>
        <div className="flex flex-wrap gap-2">
          <Button
            variant="outline"
            disabled={status !== 'success' || isBusy || mode !== 'closed'}
            onClick={openCreate}
          >
            <Plus className="size-4" aria-hidden="true" />
            新建世界
          </Button>
          <Button
            variant="outline"
            disabled={status !== 'success' || isBusy || mode !== 'closed'}
            onClick={openEdit}
          >
            <Pencil className="size-4" aria-hidden="true" />
            编辑世界
          </Button>
        </div>
      </div>

      {status === 'loading' && (
        <div
          className="grid gap-2 border-t border-border pt-4"
          role="status"
          aria-label="正在加载角色世界"
        >
          <span className="h-4 w-32 animate-pulse rounded bg-surface-muted" />
          <span className="h-12 w-full animate-pulse rounded-md bg-surface-muted" />
        </div>
      )}

      {status === 'error' && (
        <div
          className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-destructive/20 bg-danger-soft px-4 py-3"
          role="alert"
        >
          <p className="text-sm text-destructive">
            {errorMessage ?? '角色世界加载失败，请重试。'}
          </p>
          <Button variant="outline" onClick={onReload}>重新加载</Button>
        </div>
      )}

      {status === 'success' && (
        <div className="grid gap-3 rounded-lg border border-border bg-surface-muted px-4 py-4">
          <div className="flex min-w-0 items-start gap-3">
            <span className="grid size-9 shrink-0 place-items-center rounded-md bg-primary-soft text-primary">
              <Globe2 className="size-[18px]" aria-hidden="true" />
            </span>
            <div className="min-w-0">
              <p className="font-semibold text-foreground">
                {selectedWorld.name}
              </p>
              <p className="mt-1 max-w-3xl whitespace-pre-wrap text-sm leading-6 text-muted-foreground">
                {selectedWorld.description || '这个世界暂未填写详细说明。'}
              </p>
            </div>
          </div>
          <p className="flex items-center gap-2 border-t border-border pt-3 text-xs leading-5 text-muted-foreground">
            <UsersRound className="size-4 shrink-0" aria-hidden="true" />
            {usageCount > 1
              ? `共有 ${usageCount} 位好友使用这个世界，修改说明会同时影响他们。`
              : '当前只有这位好友使用这个世界。'}
          </p>
        </div>
      )}

      {mode !== 'closed' && (
        <fieldset
          className="grid gap-4 border-t border-border pt-5"
          disabled={isBusy}
        >
          <legend className="sr-only">
            {mode === 'create' ? '新建角色世界' : '编辑角色世界'}
          </legend>
          <div>
            <h5 className="text-sm font-semibold text-foreground">
              {mode === 'create' ? '新建角色世界' : `编辑“${selectedWorld.name}”`}
            </h5>
            <p className="mt-1 text-xs leading-5 text-muted-foreground">
              世界说明是角色理解自身环境时的最高优先级设定。
            </p>
          </div>
          <div className="grid gap-2">
            <label
              className="text-sm font-semibold text-foreground"
              htmlFor="character-world-name"
            >
              世界名称
            </label>
            <input
              id="character-world-name"
              autoFocus
              name="characterWorldName"
              autoComplete="off"
              className="form-control"
              maxLength={100}
              required
              value={draft.name}
              onChange={(event) => setDraft((current) => ({
                ...current,
                name: event.target.value,
              }))}
            />
          </div>
          <div className="grid gap-2">
            <label
              className="text-sm font-semibold text-foreground"
              htmlFor="character-world-description"
            >
              世界设定说明
            </label>
            <textarea
              id="character-world-description"
              name="characterWorldDescription"
              autoComplete="off"
              className="form-control min-h-28 resize-y"
              rows={5}
              maxLength={4000}
              aria-describedby="character-world-description-count"
              value={draft.description}
              onChange={(event) => setDraft((current) => ({
                ...current,
                description: event.target.value,
              }))}
            />
            <span
              id="character-world-description-count"
              className="justify-self-end text-xs font-normal text-muted-foreground"
            >
              {draft.description.length}/4000
            </span>
          </div>
          {mutationErrorMessage && (
            <p
              className="rounded-lg border border-destructive/20 bg-danger-soft px-4 py-3 text-sm text-destructive"
              role="alert"
            >
              {mutationErrorMessage}
            </p>
          )}
          <div className="flex justify-end gap-2">
            <Button variant="ghost" disabled={isBusy} onClick={closeEditor}>
              <X className="size-4" aria-hidden="true" />
              取消
            </Button>
            <Button disabled={!canSaveWorld} onClick={() => void saveWorld()}>
              {isBusy
                ? <LoaderCircle className="size-4 animate-spin" aria-hidden="true" />
                : <Save className="size-4" aria-hidden="true" />}
              {isBusy ? '正在保存…' : '保存世界'}
            </Button>
          </div>
        </fieldset>
      )}
    </div>
  )
}
