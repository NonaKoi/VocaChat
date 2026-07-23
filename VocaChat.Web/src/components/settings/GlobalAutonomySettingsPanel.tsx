import { useEffect, useMemo, useState } from 'react'
import { CircleGauge, MessageCircleMore, Save, Server, TimerReset, Users, UsersRound } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import type {
  AutonomousInteractionSettingsResponse,
  AiModelConnectionSettingsResponse,
  UpdateAiModelConnectionSettingsRequest,
  UpdateAutonomousInteractionSettingsRequest,
} from '@/api/types'
import { AiModelConnectionFields } from '@/components/settings/AiModelConnectionFields'
import { isAiModelConnectionValid } from '@/components/settings/aiModelConnectionForm'
import { ErrorState } from '@/components/feedback/ErrorState'
import { SettingsLevelSelector } from '@/components/settings/SettingsLevelSelector'
import { ReplyTimingFields } from '@/components/settings/ReplyTimingFields'
import {
  ReplyMessageCountFields,
} from '@/components/settings/ReplyMessageCountFields'
import { isReplyMessageCountRangeValid } from '@/components/settings/replyMessageCount'
import {
  formatReplyTiming,
  isReplyTimingValid,
} from '@/components/settings/replyTiming'
import { getLevelLabel } from '@/components/settings/settingsLabels'
import { SettingsToggle } from '@/components/settings/SettingsToggle'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useAutonomousInteractionSettings } from '@/hooks/useAutonomousInteractionSettings'
import { useAiModelConnectionSettings } from '@/hooks/useAiModelConnectionSettings'
import { useTokenUsageVisibility } from '@/hooks/useTokenUsageVisibility'
import { cn } from '@/lib/utils'

interface GlobalAutonomySettingsPanelProps {
  onDirtyChange: (hasChanges: boolean) => void
}

export function GlobalAutonomySettingsPanel({
  onDirtyChange,
}: GlobalAutonomySettingsPanelProps) {
  const settings = useAutonomousInteractionSettings()
  const modelSettings = useAiModelConnectionSettings()
  const tokenUsageVisibility = useTokenUsageVisibility()
  const [draft, setDraft] = useState<UpdateAutonomousInteractionSettingsRequest>()
  const [modelDraft, setModelDraft] = useState<UpdateAiModelConnectionSettingsRequest>()
  const [didSave, setDidSave] = useState(false)

  useEffect(() => {
    if (settings.data) setDraft(settings.data)
  }, [settings.data])

  useEffect(() => {
    if (modelSettings.data) {
      setModelDraft(toModelUpdateRequest(modelSettings.data))
    }
  }, [modelSettings.data])

  const hasChanges = useMemo(
    () => Boolean(
      draft
      && settings.data
      && !areSettingsEqual(draft, settings.data),
    ),
    [draft, settings.data],
  )
  const modelHasChanges = useMemo(
    () => Boolean(
      modelDraft
      && modelSettings.data
      && !areModelSettingsEqual(modelDraft, modelSettings.data),
    ),
    [modelDraft, modelSettings.data],
  )
  const anyChanges = hasChanges || modelHasChanges
  const settingsAreValid = Boolean(
    draft
    && Number.isInteger(draft.privateChatContinuationRatePercent)
    && draft.privateChatContinuationRatePercent >= 0
    && draft.privateChatContinuationRatePercent <= 95
    && Number.isInteger(draft.privateChatMaximumRounds)
    && draft.privateChatMaximumRounds >= 1
    && draft.privateChatMaximumRounds <= 12
    && Number.isInteger(draft.autonomousGroupChatMaximumMembers)
    && draft.autonomousGroupChatMaximumMembers >= 3
    && Number.isInteger(draft.groupChatContinuationRatePercent)
    && draft.groupChatContinuationRatePercent >= 0
    && draft.groupChatContinuationRatePercent <= 95
    && Number.isInteger(draft.groupChatMaximumRounds)
    && draft.groupChatMaximumRounds >= 1
    && draft.groupChatMaximumRounds <= 12
    && Number.isInteger(draft.groupChatMaximumSpeakersPerTurn)
    && draft.groupChatMaximumSpeakersPerTurn >= 1
    && draft.groupChatMaximumSpeakersPerTurn <= 12
    && Number.isInteger(draft.groupChatWholeGroupMaximumSpeakersPerTurn)
    && draft.groupChatWholeGroupMaximumSpeakersPerTurn >= 1
    && draft.groupChatWholeGroupMaximumSpeakersPerTurn <= 12
    && Number.isInteger(draft.groupChatMaximumMessagesPerTurn)
    && draft.groupChatMaximumMessagesPerTurn >= 1
    && draft.groupChatMaximumMessagesPerTurn <= 12
    && draft.groupChatMaximumSpeakersPerTurn <= draft.groupChatMaximumMessagesPerTurn
    && draft.groupChatWholeGroupMaximumSpeakersPerTurn <= draft.groupChatMaximumMessagesPerTurn
    && isReplyTimingValid(draft)
    && isReplyTimingValid({
      fixedReplyDelayMilliseconds: draft.fixedConsecutiveMessageDelayMilliseconds,
      minimumReplyDelayMilliseconds: draft.minimumConsecutiveMessageDelayMilliseconds,
      maximumReplyDelayMilliseconds: draft.maximumConsecutiveMessageDelayMilliseconds,
    })
    && Number.isInteger(draft.maximumConsecutiveQuestionTurns)
    && draft.maximumConsecutiveQuestionTurns >= 1
    && isReplyMessageCountRangeValid(
      draft.minimumReplyMessageCount,
      draft.maximumReplyMessageCount,
    )
    && modelDraft
    && isAiModelConnectionValid(modelDraft),
  )

  useEffect(() => {
    onDirtyChange(anyChanges)
  }, [anyChanges, onDirtyChange])

  function updateDraft(
    values: Partial<UpdateAutonomousInteractionSettingsRequest>,
  ) {
    setDidSave(false)
    setDraft((current) => current ? { ...current, ...values } : current)
  }

  function updateModelDraft(
    values: Partial<UpdateAiModelConnectionSettingsRequest>,
  ) {
    setDidSave(false)
    setModelDraft((current) => current ? { ...current, ...values } : current)
  }

  async function saveChanges() {
    if (!draft || !modelDraft) return

    if (modelHasChanges) {
      const savedModelSettings = await modelSettings.save(modelDraft)
      if (!savedModelSettings) return
      setModelDraft(toModelUpdateRequest(savedModelSettings))
    }

    if (hasChanges) {
      const savedSettings = await settings.save(draft)
      if (!savedSettings) return
      setDraft(savedSettings)
    }

    setDidSave(true)
  }

  if (settings.status === 'loading' || modelSettings.status === 'loading') {
    return <SettingsLoadingState />
  }

  if (settings.status === 'error' || modelSettings.status === 'error') {
    return (
      <div className="max-w-xl">
        <ErrorState
          message={settings.errorMessage ?? modelSettings.errorMessage}
          onRetry={() => void Promise.all([
            settings.reload(),
            modelSettings.reload(),
          ])}
        />
      </div>
    )
  }

  if (!draft || !modelDraft) return null

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
          <AiModelConnectionFields
            idPrefix="global-ai-model"
            draft={modelDraft}
            hasApiKey={modelSettings.data?.hasApiKey ?? false}
            onChange={updateModelDraft}
          />

          <SettingsToggle
            id="show-token-usage"
            label="显示消息 Token 消耗"
            description="在 AI 消息下显示群级导演、单人导演和回复生成的实际 Token 用量。"
            checked={tokenUsageVisibility.isVisible}
            onCheckedChange={tokenUsageVisibility.setIsVisible}
          />

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

          <ReplyTimingFields
            idPrefix="global-reply-timing"
            title="回复等待"
            description="控制收到对方消息后，好友等待多久再开始回复；模型生成耗时会计入等待。"
            mode={draft.replyDelayMode}
            fixedDelayMilliseconds={draft.fixedReplyDelayMilliseconds}
            minimumDelayMilliseconds={draft.minimumReplyDelayMilliseconds}
            maximumDelayMilliseconds={draft.maximumReplyDelayMilliseconds}
            onModeChange={(replyDelayMode) => updateDraft({ replyDelayMode })}
            onFixedDelayChange={(fixedReplyDelayMilliseconds) => updateDraft({ fixedReplyDelayMilliseconds })}
            onMinimumDelayChange={(minimumReplyDelayMilliseconds) => updateDraft({ minimumReplyDelayMilliseconds })}
            onMaximumDelayChange={(maximumReplyDelayMilliseconds) => updateDraft({ maximumReplyDelayMilliseconds })}
          />

          <ReplyTimingFields
            idPrefix="global-consecutive-message-timing"
            title="同次多条消息间隔"
            description="控制同一位好友一次回复拆成多条消息时，相邻消息之间的发送间隔。"
            mode={draft.consecutiveMessageDelayMode}
            fixedDelayMilliseconds={draft.fixedConsecutiveMessageDelayMilliseconds}
            minimumDelayMilliseconds={draft.minimumConsecutiveMessageDelayMilliseconds}
            maximumDelayMilliseconds={draft.maximumConsecutiveMessageDelayMilliseconds}
            onModeChange={(consecutiveMessageDelayMode) => updateDraft({ consecutiveMessageDelayMode })}
            onFixedDelayChange={(fixedConsecutiveMessageDelayMilliseconds) => updateDraft({ fixedConsecutiveMessageDelayMilliseconds })}
            onMinimumDelayChange={(minimumConsecutiveMessageDelayMilliseconds) => updateDraft({ minimumConsecutiveMessageDelayMilliseconds })}
            onMaximumDelayChange={(maximumConsecutiveMessageDelayMilliseconds) => updateDraft({ maximumConsecutiveMessageDelayMilliseconds })}
          />

          <ReplyMessageCountFields
            idPrefix="global-reply-message-count"
            minimum={draft.minimumReplyMessageCount}
            maximum={draft.maximumReplyMessageCount}
            onMinimumChange={(minimumReplyMessageCount) => updateDraft({ minimumReplyMessageCount })}
            onMaximumChange={(maximumReplyMessageCount) => updateDraft({ maximumReplyMessageCount })}
          />

          <SettingsNumberField
            id="group-chat-maximum-speakers-per-turn"
            label="普通群消息最多回复好友"
            description="用户发送普通群消息时，单轮最多安排多少位好友参与回复。"
            value={draft.groupChatMaximumSpeakersPerTurn}
            min={1}
            max={12}
            suffix="人"
            disabled={false}
            onValueChange={(groupChatMaximumSpeakersPerTurn) => updateDraft({ groupChatMaximumSpeakersPerTurn })}
          />

          <SettingsNumberField
            id="group-chat-whole-group-maximum-speakers-per-turn"
            label="面向全群最多发言好友"
            description="消息面向大家、点名多位好友或好友自主群聊时，单轮最多安排多少位好友发言。"
            value={draft.groupChatWholeGroupMaximumSpeakersPerTurn}
            min={1}
            max={12}
            suffix="人"
            disabled={false}
            onValueChange={(groupChatWholeGroupMaximumSpeakersPerTurn) => updateDraft({ groupChatWholeGroupMaximumSpeakersPerTurn })}
          />

          <SettingsNumberField
            id="group-chat-maximum-messages-per-turn"
            label="单轮 AI 消息总量"
            description="同一轮所有好友合计最多发送的消息数；该值不能小于上面的任一发言人数。"
            value={draft.groupChatMaximumMessagesPerTurn}
            min={1}
            max={12}
            suffix="条"
            disabled={false}
            errorMessage={draft.groupChatMaximumMessagesPerTurn
                < Math.max(
                  draft.groupChatMaximumSpeakersPerTurn,
                  draft.groupChatWholeGroupMaximumSpeakersPerTurn,
                )
              ? '单轮 AI 消息总量不能小于上面的任一发言人数。'
              : undefined}
            onValueChange={(groupChatMaximumMessagesPerTurn) => updateDraft({ groupChatMaximumMessagesPerTurn })}
          />

          <SettingsNumberField
            id="maximum-consecutive-question-turns"
            label="连续疑问轮次上限"
            description="同一好友连续以问题收尾达到此轮数后，下一轮由导演强制使用陈述语气。"
            value={draft.maximumConsecutiveQuestionTurns}
            min={1}
            suffix="轮"
            disabled={false}
            onValueChange={(maximumConsecutiveQuestionTurns) => updateDraft({ maximumConsecutiveQuestionTurns })}
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

          <SettingsNumberField
            id="autonomous-group-chat-maximum-members"
            label="单次好友群聊最大人数"
            description="自主组成好友群聊时，至少会有 3 名好友参与；这里设置单次群聊允许的最多人数。"
            value={draft.autonomousGroupChatMaximumMembers}
            min={3}
            suffix="人"
            disabled={!draft.isEnabled || !draft.allowGroupChats}
            onValueChange={(autonomousGroupChatMaximumMembers) => updateDraft({ autonomousGroupChatMaximumMembers })}
          />

          <SettingsNumberField
            id="group-chat-continuation-rate"
            label="好友群聊下一轮概率保留比例"
            description="每完成一轮后，下一轮的基础概率按此比例递减，并继续接受群关系和参与情况修正。"
            value={draft.groupChatContinuationRatePercent}
            min={0}
            max={95}
            suffix="%"
            disabled={!draft.isEnabled || !draft.allowGroupChats}
            onValueChange={(groupChatContinuationRatePercent) => updateDraft({ groupChatContinuationRatePercent })}
          />

          <SettingsNumberField
            id="group-chat-maximum-rounds"
            label="单次好友群聊最大轮数"
            description="达到此普通轮数后会强制进入一次收束，避免群聊无限持续。"
            value={draft.groupChatMaximumRounds}
            min={1}
            max={12}
            suffix="轮"
            disabled={!draft.isEnabled || !draft.allowGroupChats}
            onValueChange={(groupChatMaximumRounds) => updateDraft({ groupChatMaximumRounds })}
          />
        </div>

        <SettingsSaveBar
          hasChanges={anyChanges}
          canSave={settingsAreValid}
          didSave={didSave}
          isSaving={settings.isSaving || modelSettings.isSaving}
          errorMessage={settings.saveErrorMessage ?? modelSettings.saveErrorMessage}
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
            icon={Server}
            label="AI 模型"
            value={modelDraft.model || '未配置'}
          />
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
            label="回复间隔"
            value={formatReplyTiming(draft)}
          />
          <StatusRow
            icon={TimerReset}
            label="连发间隔"
            value={formatReplyTiming({
              replyDelayMode: draft.consecutiveMessageDelayMode,
              fixedReplyDelayMilliseconds: draft.fixedConsecutiveMessageDelayMilliseconds,
              minimumReplyDelayMilliseconds: draft.minimumConsecutiveMessageDelayMilliseconds,
              maximumReplyDelayMilliseconds: draft.maximumConsecutiveMessageDelayMilliseconds,
            })}
          />
          <StatusRow
            icon={MessageCircleMore}
            label="疑问节奏"
            value={`连续 ${draft.maximumConsecutiveQuestionTurns} 轮后强制陈述`}
          />
          <StatusRow
            icon={TimerReset}
            label="单次私信"
            value={`${draft.privateChatContinuationRatePercent}% · 最多 ${draft.privateChatMaximumRounds} 轮`}
          />
          <StatusRow
            icon={Users}
            label="单次好友群聊"
            value={`3–${draft.autonomousGroupChatMaximumMembers} 人 · ${draft.groupChatContinuationRatePercent}% · 最多 ${draft.groupChatMaximumRounds} 轮`}
          />
          <StatusRow
            icon={MessageCircleMore}
            label="群聊单轮密度"
            value={`普通 ${draft.groupChatMaximumSpeakersPerTurn} 人 · 全群 ${draft.groupChatWholeGroupMaximumSpeakersPerTurn} 人 · ${draft.groupChatMaximumMessagesPerTurn} 条`}
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

export function SettingsNumberField({
  id,
  label,
  description,
  value,
  min,
  max,
  suffix,
  disabled,
  errorMessage,
  onValueChange,
}: {
  id: string
  label: string
  description: string
  value: number
  min: number
  max?: number
  suffix: string
  disabled: boolean
  errorMessage?: string
  onValueChange: (value: number) => void
}) {
  const hasInvalidNumber = !Number.isInteger(value)
    || value < min
    || (max !== undefined && value > max)
  const isInvalid = hasInvalidNumber || Boolean(errorMessage)
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
            {errorMessage ?? (max === undefined
              ? `请输入不小于 ${min} 的整数。`
              : `请输入 ${min} 到 ${max} 之间的整数。`)}
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
    && first.autonomousGroupChatMaximumMembers === second.autonomousGroupChatMaximumMembers
    && first.groupChatContinuationRatePercent === second.groupChatContinuationRatePercent
    && first.groupChatMaximumRounds === second.groupChatMaximumRounds
    && first.replyDelayMode === second.replyDelayMode
    && first.fixedReplyDelayMilliseconds === second.fixedReplyDelayMilliseconds
    && first.minimumReplyDelayMilliseconds === second.minimumReplyDelayMilliseconds
    && first.maximumReplyDelayMilliseconds === second.maximumReplyDelayMilliseconds
    && first.consecutiveMessageDelayMode === second.consecutiveMessageDelayMode
    && first.fixedConsecutiveMessageDelayMilliseconds === second.fixedConsecutiveMessageDelayMilliseconds
    && first.minimumConsecutiveMessageDelayMilliseconds === second.minimumConsecutiveMessageDelayMilliseconds
    && first.maximumConsecutiveMessageDelayMilliseconds === second.maximumConsecutiveMessageDelayMilliseconds
    && first.maximumConsecutiveQuestionTurns === second.maximumConsecutiveQuestionTurns
    && first.minimumReplyMessageCount === second.minimumReplyMessageCount
    && first.maximumReplyMessageCount === second.maximumReplyMessageCount
    && first.groupChatMaximumSpeakersPerTurn === second.groupChatMaximumSpeakersPerTurn
    && first.groupChatWholeGroupMaximumSpeakersPerTurn === second.groupChatWholeGroupMaximumSpeakersPerTurn
    && first.groupChatMaximumMessagesPerTurn === second.groupChatMaximumMessagesPerTurn
}

function areModelSettingsEqual(
  first: UpdateAiModelConnectionSettingsRequest,
  second: AiModelConnectionSettingsResponse,
) {
  return first.baseUrl === second.baseUrl
    && first.model === second.model
    && first.apiKey.length === 0
    && !first.clearApiKey
}

function toModelUpdateRequest(
  settings: AiModelConnectionSettingsResponse,
): UpdateAiModelConnectionSettingsRequest {
  return {
    baseUrl: settings.baseUrl,
    model: settings.model,
    apiKey: '',
    clearApiKey: false,
  }
}

function getScopeLabel(settings: UpdateAutonomousInteractionSettingsRequest) {
  if (!settings.isEnabled) return '暂不发生'
  if (settings.allowPrivateChats && settings.allowGroupChats) return '私信与群聊'
  if (settings.allowPrivateChats) return '仅私信'
  if (settings.allowGroupChats) return '仅群聊'
  return '未选择'
}
