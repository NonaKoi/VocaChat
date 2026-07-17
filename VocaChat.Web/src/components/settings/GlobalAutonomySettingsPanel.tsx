import { useEffect, useMemo, useState } from 'react'
import { CircleGauge, MessageCircleMore, Save, UsersRound } from 'lucide-react'
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
  didSave,
  isSaving,
  errorMessage,
  onSave,
}: {
  hasChanges: boolean
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
      <Button onClick={onSave} disabled={!hasChanges || isSaving}>
        <Save className="size-4" strokeWidth={1.8} aria-hidden="true" />
        {isSaving ? '正在保存…' : '保存更改'}
      </Button>
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
}

function getScopeLabel(settings: UpdateAutonomousInteractionSettingsRequest) {
  if (!settings.isEnabled) return '暂不发生'
  if (settings.allowPrivateChats && settings.allowGroupChats) return '私信与群聊'
  if (settings.allowPrivateChats) return '仅私信'
  if (settings.allowGroupChats) return '仅群聊'
  return '未选择'
}
