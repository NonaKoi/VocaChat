import { useEffect, useMemo, useState } from 'react'
import { CircleGauge, MessageCircleMore, Save, TimerReset, UsersRound } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import type {
  AutonomousInteractionSettingsResponse,
  UpdateAutonomousInteractionSettingsRequest,
} from '@/api/types'
import { ErrorState } from '@/components/feedback/ErrorState'
import { SettingsLevelSelector } from '@/components/settings/SettingsLevelSelector'
import { getLevelLabel } from '@/components/settings/settingsLabels'
import { SettingsToggle } from '@/components/settings/SettingsToggle'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useAutonomousInteractionSettings } from '@/hooks/useAutonomousInteractionSettings'
import { cn } from '@/lib/utils'

interface GlobalAutonomySettingsPanelProps {
  onDirtyChange: (hasChanges: boolean) => void
}

export function GlobalAutonomySettingsPanel({
  onDirtyChange,
}: GlobalAutonomySettingsPanelProps) {
  const settings = useAutonomousInteractionSettings()
  const [draft, setDraft] = useState<UpdateAutonomousInteractionSettingsRequest>()
  const [didSave, setDidSave] = useState(false)

  useEffect(() => {
    if (settings.data) setDraft(settings.data)
  }, [settings.data])

  const hasChanges = useMemo(
    () => Boolean(
      draft
      && settings.data
      && !areSettingsEqual(draft, settings.data),
    ),
    [draft, settings.data],
  )
  const settingsAreValid = Boolean(
    draft
    && Number.isInteger(draft.privateChatContinuationRatePercent)
    && draft.privateChatContinuationRatePercent >= 0
    && draft.privateChatContinuationRatePercent <= 95
    && Number.isInteger(draft.privateChatMaximumRounds)
    && draft.privateChatMaximumRounds >= 1
    && draft.privateChatMaximumRounds <= 12,
  )

  useEffect(() => {
    onDirtyChange(hasChanges)
  }, [hasChanges, onDirtyChange])

  function updateDraft(
    values: Partial<UpdateAutonomousInteractionSettingsRequest>,
  ) {
    setDidSave(false)
    setDraft((current) => current ? { ...current, ...values } : current)
  }

  async function saveChanges() {
    if (!draft) return
    const saved = await settings.save(draft)
    if (saved) {
      setDraft(saved)
      setDidSave(true)
    }
  }

  if (settings.status === 'loading') return <SettingsLoadingState />

  if (settings.status === 'error') {
    return (
      <div className="max-w-xl">
        <ErrorState message={settings.errorMessage} onRetry={settings.reload} />
      </div>
    )
  }

  if (!draft) return null

  return (
    <div className="grid items-start gap-5 xl:grid-cols-[minmax(0,1fr)_250px]">
      <section
        className="overflow-hidden rounded-xl border border-border bg-surface"
        aria-labelledby="global-autonomy-settings-title"
      >
        <div className="border-b border-border px-5 py-4">
          <h3
            id="global-autonomy-settings-title"
            className="text-base font-semibold text-foreground"
          >
            通用设置
          </h3>
          <p className="mt-1 text-xs leading-5 text-muted-foreground">
            统一管理所有好友的自主互动范围。
          </p>
        </div>

        <div className="divide-y divide-border">
          <SettingsToggle
            id="autonomous-interactions-enabled"
            label="允许好友自主互动"
            description="统一控制好友之间是否可以发生自主互动。"
            checked={draft.isEnabled}
            onCheckedChange={(checked) => updateDraft({ isEnabled: checked })}
          />

          <SettingsLevelSelector
            label="发生频率"
            description="控制整体互动活跃程度。"
            value={draft.frequency}
            disabled={!draft.isEnabled}
            onValueChange={(frequency) => updateDraft({ frequency })}
          />

          <SettingsToggle
            id="autonomous-private-chats-enabled"
            label="允许好友自主发起私信"
            description="允许好友之间主动建立或继续一对一会话。"
            checked={draft.allowPrivateChats}
            disabled={!draft.isEnabled}
            onCheckedChange={(checked) => updateDraft({ allowPrivateChats: checked })}
          />

          <SettingsNumberField
            id="private-chat-continuation-rate"
            label="下一轮概率保留比例"
            description="每完成一轮后，下一轮的基础概率按此比例递减；关系和回应情况还会继续修正。"
            value={draft.privateChatContinuationRatePercent}
            min={0}
            max={95}
            suffix="%"
            disabled={!draft.isEnabled || !draft.allowPrivateChats}
            onValueChange={(privateChatContinuationRatePercent) => updateDraft({ privateChatContinuationRatePercent })}
          />

          <SettingsNumberField
            id="private-chat-maximum-rounds"
            label="单次私信最大轮数"
            description="即使概率判断持续通过，普通交流达到此轮数后也会进入收束。"
            value={draft.privateChatMaximumRounds}
            min={1}
            max={12}
            suffix="轮"
            disabled={!draft.isEnabled || !draft.allowPrivateChats}
            onValueChange={(privateChatMaximumRounds) => updateDraft({ privateChatMaximumRounds })}
          />

          <SettingsToggle
            id="autonomous-group-chats-enabled"
            label="允许好友自主组成群聊"
            description="允许好友自主创建不包含本地用户的群聊。"
            checked={draft.allowGroupChats}
            disabled={!draft.isEnabled}
            onCheckedChange={(checked) => updateDraft({ allowGroupChats: checked })}
          />
        </div>

        <SettingsSaveBar
          hasChanges={hasChanges}
          canSave={settingsAreValid}
          didSave={didSave}
          isSaving={settings.isSaving}
          errorMessage={settings.saveErrorMessage}
          onSave={() => void saveChanges()}
        />
      </section>

      <aside
        className="overflow-hidden rounded-xl border border-border bg-surface"
        aria-label="当前通用设置状态"
      >
        <div className="border-b border-border px-5 py-4">
          <h3 className="text-base font-semibold text-foreground">当前状态</h3>
        </div>
        <dl className="divide-y divide-border px-5">
          <StatusRow
            icon={CircleGauge}
            label="自主互动"
            value={draft.isEnabled ? '已开启' : '已关闭'}
            tone={draft.isEnabled ? 'success' : 'muted'}
          />
          <StatusRow
            icon={MessageCircleMore}
            label="允许范围"
            value={getScopeLabel(draft)}
          />
          <StatusRow
            icon={UsersRound}
            label="频率档位"
            value={getLevelLabel(draft.frequency)}
          />
          <StatusRow
            icon={TimerReset}
            label="单次私信"
            value={`${draft.privateChatContinuationRatePercent}% · 最多 ${draft.privateChatMaximumRounds} 轮`}
          />
        </dl>
        <p className="m-4 rounded-lg bg-primary-soft p-3 text-xs leading-5 text-primary">
          总开关优先于好友设置。关闭后，所有好友的专有设置都会暂停生效。
        </p>
      </aside>
    </div>
  )
}

export function SettingsSaveBar({
  hasChanges,
  canSave = true,
  didSave,
  isSaving,
  errorMessage,
  onSave,
}: {
  hasChanges: boolean
  canSave?: boolean
  didSave: boolean
  isSaving: boolean
  errorMessage?: string
  onSave: () => void
}) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-border bg-surface-muted px-5 py-4">
      <p className="text-xs text-muted-foreground" aria-live="polite">
        {errorMessage ? (
          <span className="text-destructive">{errorMessage}</span>
        ) : didSave
          ? '设置已保存到本地数据库。'
          : hasChanges
            ? '有尚未保存的更改。'
            : '当前没有未保存的更改。'}
      </p>
      <Button onClick={onSave} disabled={!hasChanges || !canSave || isSaving}>
        <Save className="size-4" strokeWidth={1.8} aria-hidden="true" />
        {isSaving ? '正在保存…' : '保存更改'}
      </Button>
    </div>
  )
}

function SettingsNumberField({
  id,
  label,
  description,
  value,
  min,
  max,
  suffix,
  disabled,
  onValueChange,
}: {
  id: string
  label: string
  description: string
  value: number
  min: number
  max: number
  suffix: string
  disabled: boolean
  onValueChange: (value: number) => void
}) {
  const isInvalid = !Number.isInteger(value) || value < min || value > max
  const descriptionId = `${id}-description`
  const errorId = `${id}-error`

  return (
    <div className="flex flex-wrap items-center justify-between gap-4 px-5 py-4">
      <div className="min-w-0 flex-1">
        <label htmlFor={id} className="text-sm font-medium text-foreground">
          {label}
        </label>
        <p id={descriptionId} className="mt-1 max-w-2xl text-xs leading-5 text-muted-foreground">
          {description}
        </p>
        {isInvalid && (
          <p id={errorId} className="mt-1 text-xs text-destructive" role="alert">
            请输入 {min} 到 {max} 之间的整数。
          </p>
        )}
      </div>
      <div className="flex h-9 w-28 shrink-0 items-center overflow-hidden rounded-lg border border-border bg-surface focus-within:border-primary/50 focus-within:ring-2 focus-within:ring-ring/30">
        <input
          id={id}
          name={id}
          type="number"
          inputMode="numeric"
          autoComplete="off"
          min={min}
          max={max}
          step={1}
          value={value}
          disabled={disabled}
          aria-invalid={isInvalid}
          aria-describedby={`${descriptionId}${isInvalid ? ` ${errorId}` : ''}`}
          onChange={(event) => onValueChange(Number(event.target.value))}
          className="min-w-0 flex-1 bg-transparent px-3 text-right text-sm tabular-nums text-foreground outline-none disabled:cursor-not-allowed disabled:text-muted-foreground"
        />
        <span className="border-l border-border px-2 text-xs text-muted-foreground" aria-hidden="true">
          {suffix}
        </span>
      </div>
    </div>
  )
}

export function StatusRow({
  icon: Icon,
  label,
  value,
  tone = 'default',
}: {
  icon: LucideIcon
  label: string
  value: string
  tone?: 'default' | 'success' | 'muted'
}) {
  return (
    <div className="flex items-center gap-3 py-4">
      <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-surface-muted text-primary">
        <Icon className="size-[18px]" strokeWidth={1.75} aria-hidden="true" />
      </span>
      <div className="min-w-0">
        <dt className="text-xs text-muted-foreground">{label}</dt>
        <dd
          className={cn(
            'mt-0.5 truncate text-sm font-semibold text-foreground',
            tone === 'success' && 'text-success',
            tone === 'muted' && 'text-muted-foreground',
          )}
        >
          {value}
        </dd>
      </div>
    </div>
  )
}

function SettingsLoadingState() {
  return (
    <div
      className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_250px]"
      role="status"
      aria-label="正在加载好友自主互动设置"
    >
      <Skeleton className="h-[430px] rounded-xl" />
      <Skeleton className="h-72 rounded-xl" />
    </div>
  )
}

function areSettingsEqual(
  first: UpdateAutonomousInteractionSettingsRequest,
  second: AutonomousInteractionSettingsResponse,
) {
  return first.isEnabled === second.isEnabled
    && first.frequency === second.frequency
    && first.allowPrivateChats === second.allowPrivateChats
    && first.allowGroupChats === second.allowGroupChats
    && first.privateChatContinuationRatePercent === second.privateChatContinuationRatePercent
    && first.privateChatMaximumRounds === second.privateChatMaximumRounds
}

function getScopeLabel(settings: UpdateAutonomousInteractionSettingsRequest) {
  if (!settings.isEnabled) return '暂不发生'
  if (settings.allowPrivateChats && settings.allowGroupChats) return '私信与群聊'
  if (settings.allowPrivateChats) return '仅私信'
  if (settings.allowGroupChats) return '仅群聊'
  return '未选择'
}
