import { useEffect, useMemo, useState } from 'react'
import {
  CircleGauge,
  MessageCircleMore,
  Search,
  Server,
  TimerReset,
  UserRound,
  UsersRound,
} from 'lucide-react'
import type {
  AiAccountAutonomySettingsResponse,
  AiAccountModelConnectionSettingsResponse,
  ContactResponse,
  UpdateAiAccountModelConnectionSettingsRequest,
  UpdateAiAccountAutonomySettingsRequest,
} from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { EmptyState } from '@/components/feedback/EmptyState'
import { ErrorState } from '@/components/feedback/ErrorState'
import {
  SettingsSaveBar,
  SettingsNumberField,
  StatusRow,
} from '@/components/settings/GlobalAutonomySettingsPanel'
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
import { AiModelConnectionFields } from '@/components/settings/AiModelConnectionFields'
import { isAiModelConnectionValid } from '@/components/settings/aiModelConnectionForm'
import { Skeleton } from '@/components/ui/skeleton'
import { useAiAccountAutonomySettings } from '@/hooks/useAiAccountAutonomySettings'
import { useAiAccountModelConnectionSettings } from '@/hooks/useAiAccountModelConnectionSettings'
import { cn } from '@/lib/utils'
import type { RemoteStatus } from '@/types/remoteStatus'

interface FriendAutonomySettingsPanelProps {
  contacts: ContactResponse[]
  contactStatus: RemoteStatus
  contactErrorMessage?: string
  onReloadContacts: () => void | Promise<void>
  onDirtyChange: (hasChanges: boolean) => void
}

/** 管理单个好友的自主互动权限，不在前端复制后端业务判断。 */
export function FriendAutonomySettingsPanel({
  contacts,
  contactStatus,
  contactErrorMessage,
  onReloadContacts,
  onDirtyChange,
}: FriendAutonomySettingsPanelProps) {
  const [searchTerm, setSearchTerm] = useState('')
  const [selectedAccountId, setSelectedAccountId] = useState<string | undefined>(
    () => new URLSearchParams(window.location.search).get('friendSetting') ?? undefined,
  )
  const selectedContact = contacts.find(
    (contact) => contact.friend.id === selectedAccountId,
  )
  const settings = useAiAccountAutonomySettings(selectedContact?.friend.id)
  const modelSettings = useAiAccountModelConnectionSettings(
    selectedContact?.friend.id,
  )
  const [draft, setDraft] = useState<UpdateAiAccountAutonomySettingsRequest>()
  const [modelDraft, setModelDraft] = useState<UpdateAiAccountModelConnectionSettingsRequest>()
  const [didSave, setDidSave] = useState(false)

  useEffect(() => {
    if (contacts.length === 0) {
      setSelectedAccountId(undefined)
      return
    }

    if (!contacts.some((contact) => contact.friend.id === selectedAccountId)) {
      setSelectedAccountId(contacts[0].friend.id)
    }
  }, [contacts, selectedAccountId])

  useEffect(() => {
    setDidSave(false)
    setDraft(settings.data ? toUpdateRequest(settings.data) : undefined)
  }, [settings.data, selectedAccountId])

  useEffect(() => {
    setDidSave(false)
    setModelDraft(
      modelSettings.data
        ? toModelUpdateRequest(modelSettings.data)
        : undefined,
    )
  }, [modelSettings.data, selectedAccountId])

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
    && isAiModelConnectionValid(
      modelDraft,
      modelDraft.useGlobalSettings,
    ),
  )

  useEffect(() => {
    onDirtyChange(anyChanges)
  }, [anyChanges, onDirtyChange])

  const visibleContacts = useMemo(() => {
    const keyword = searchTerm.trim().toLocaleLowerCase('zh-CN')
    if (!keyword) return contacts

    return contacts.filter((contact) => {
      const friend = contact.friend
      return friend.nickname.toLocaleLowerCase('zh-CN').includes(keyword)
        || friend.vcNumber.toLocaleLowerCase('zh-CN').includes(keyword)
        || friend.signature.toLocaleLowerCase('zh-CN').includes(keyword)
    })
  }, [contacts, searchTerm])

  function selectFriend(accountId: string) {
    if (accountId === selectedAccountId) return
    if (anyChanges && !window.confirm('当前好友的设置尚未保存，确定要切换吗？')) {
      return
    }

    setSelectedAccountId(accountId)
    setDraft(undefined)
    setModelDraft(undefined)
    setDidSave(false)
    const url = new URL(window.location.href)
    url.searchParams.set('friendSetting', accountId)
    window.history.replaceState(null, '', url)
  }

  function updateDraft(values: Partial<UpdateAiAccountAutonomySettingsRequest>) {
    setDidSave(false)
    setDraft((current) => current ? { ...current, ...values } : current)
  }

  function updateModelDraft(
    values: Partial<UpdateAiAccountModelConnectionSettingsRequest>,
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
      setDraft(toUpdateRequest(savedSettings))
    }

    setDidSave(true)
  }

  if (contactStatus === 'loading' || contactStatus === 'idle') {
    return <FriendSettingsLoadingState />
  }

  if (contactStatus === 'error') {
    return (
      <div className="max-w-xl">
        <ErrorState
          message={contactErrorMessage}
          onRetry={() => void onReloadContacts()}
        />
      </div>
    )
  }

  if (contacts.length === 0) {
    return (
      <div className="min-h-96 overflow-hidden rounded-xl border border-border bg-surface">
        <EmptyState
          icon={UserRound}
          title="还没有好友"
          description="添加好友后，可以在这里为每位好友设置自主互动方式。"
        />
      </div>
    )
  }

  return (
    <div className="grid min-h-[540px] overflow-hidden rounded-xl border border-border bg-surface lg:grid-cols-[270px_minmax(0,1fr)]">
      <aside className="flex min-h-0 flex-col border-b border-border bg-surface lg:border-r lg:border-b-0" aria-label="好友列表">
        <div className="border-b border-border px-4 py-4">
          <h3 className="text-sm font-semibold text-foreground">选择好友</h3>
          <label className="mt-3 flex h-9 items-center gap-2 rounded-lg border border-border bg-surface-muted px-3 text-muted-foreground focus-within:border-primary/40 focus-within:ring-2 focus-within:ring-primary/10">
            <Search className="size-4 shrink-0" strokeWidth={1.8} aria-hidden="true" />
            <span className="sr-only">搜索好友</span>
            <input
              type="search"
              name="friendSettingsSearch"
              autoComplete="off"
              value={searchTerm}
              onChange={(event) => setSearchTerm(event.target.value)}
              placeholder="搜索好友…"
              className="min-w-0 flex-1 bg-transparent text-sm text-foreground outline-none placeholder:text-muted-foreground"
            />
          </label>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto p-2">
          {visibleContacts.length === 0 ? (
            <p className="px-3 py-6 text-center text-xs text-muted-foreground">
              没有匹配的好友
            </p>
          ) : visibleContacts.map((contact) => {
            const friend = contact.friend
            const selected = friend.id === selectedAccountId
            return (
              <button
                key={contact.id}
                type="button"
                aria-current={selected ? 'true' : undefined}
                onClick={() => selectFriend(friend.id)}
                className={cn(
                  'flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left outline-none transition-colors hover:bg-surface-muted focus-visible:ring-2 focus-visible:ring-ring',
                  selected && 'bg-primary-soft',
                )}
              >
                <EntityAvatar
                  name={friend.nickname}
                  src={friend.avatarUrl}
                  size="small"
                />
                <span className="min-w-0 flex-1">
                  <span className={cn(
                    'block truncate text-sm font-medium text-foreground',
                    selected && 'text-primary',
                  )}>
                    {friend.nickname}
                  </span>
                  <span className="mt-0.5 block truncate text-xs text-muted-foreground">
                    {friend.signature || `VC号 ${friend.vcNumber}`}
                  </span>
                </span>
              </button>
            )
          })}
        </div>
      </aside>

      <div className="min-w-0 bg-surface-muted p-4 xl:p-5">
        {(settings.status === 'loading' || modelSettings.status === 'loading') && (
          <FriendDetailLoadingState />
        )}
        {(settings.status === 'error' || modelSettings.status === 'error') && (
          <div className="max-w-xl">
            <ErrorState
              message={settings.errorMessage ?? modelSettings.errorMessage}
              onRetry={() => void Promise.all([
                settings.reload(),
                modelSettings.reload(),
              ])}
            />
          </div>
        )}
        {settings.status === 'success'
          && modelSettings.status === 'success'
          && selectedContact
          && draft
          && modelDraft && (
          <div className="grid items-start gap-4 xl:grid-cols-[minmax(0,1fr)_230px]">
            <section className="overflow-hidden rounded-xl border border-border bg-surface" aria-labelledby="friend-autonomy-settings-title">
              <div className="flex items-center gap-3 border-b border-border px-5 py-4">
                <EntityAvatar
                  name={selectedContact.friend.nickname}
                  src={selectedContact.friend.avatarUrl}
                  size="small"
                />
                <div className="min-w-0">
                  <h3 id="friend-autonomy-settings-title" className="truncate text-base font-semibold text-foreground">
                    {selectedContact.friend.nickname}
                  </h3>
                  <p className="mt-0.5 text-xs text-muted-foreground">好友专有设置</p>
                </div>
              </div>

              <div className="divide-y divide-border">
                <SettingsToggle
                  id={`friend-use-global-ai-model-${selectedContact.friend.id}`}
                  label="沿用通用 AI 接口"
                  description="关闭后，这位好友的接口地址、模型和 API Key 将整套覆盖通用设置。"
                  checked={modelDraft.useGlobalSettings}
                  onCheckedChange={(checked) => updateModelDraft({
                    useGlobalSettings: checked,
                  })}
                />

                <AiModelConnectionFields
                  idPrefix={`friend-ai-model-${selectedContact.friend.id}`}
                  draft={modelDraft}
                  hasApiKey={modelSettings.data?.hasApiKey ?? false}
                  disabled={modelDraft.useGlobalSettings}
                  onChange={updateModelDraft}
                />

                <SettingsToggle
                  id={`friend-autonomy-enabled-${selectedContact.friend.id}`}
                  label="参与自主互动"
                  description="控制这位好友是否参与好友之间的自主互动。"
                  checked={draft.isEnabled}
                  onCheckedChange={(checked) => updateDraft({ isEnabled: checked })}
                />

                <SettingsLevelSelector
                  label="主动程度"
                  description="设置这位好友主动发起互动的倾向。"
                  value={draft.initiativeLevel}
                  disabled={!draft.isEnabled}
                  onValueChange={(initiativeLevel) => updateDraft({ initiativeLevel })}
                />

                <SettingsToggle
                  id={`friend-use-global-reply-delay-${selectedContact.friend.id}`}
                  label="沿用通用回复速度"
                  description="关闭后，可以为这位好友单独设置消息回复间隔。"
                  checked={draft.useGlobalReplyDelay}
                  disabled={!draft.isEnabled}
                  onCheckedChange={(checked) => updateDraft({ useGlobalReplyDelay: checked })}
                />

                <ReplyTimingFields
                  idPrefix={`friend-reply-timing-${selectedContact.friend.id}`}
                  title="回复等待"
                  description="控制收到消息后，这位好友等待多久再开始回复。"
                  mode={draft.replyDelayMode}
                  fixedDelayMilliseconds={draft.fixedReplyDelayMilliseconds}
                  minimumDelayMilliseconds={draft.minimumReplyDelayMilliseconds}
                  maximumDelayMilliseconds={draft.maximumReplyDelayMilliseconds}
                  disabled={!draft.isEnabled || draft.useGlobalReplyDelay}
                  onModeChange={(replyDelayMode) => updateDraft({ replyDelayMode })}
                  onFixedDelayChange={(fixedReplyDelayMilliseconds) => updateDraft({ fixedReplyDelayMilliseconds })}
                  onMinimumDelayChange={(minimumReplyDelayMilliseconds) => updateDraft({ minimumReplyDelayMilliseconds })}
                  onMaximumDelayChange={(maximumReplyDelayMilliseconds) => updateDraft({ maximumReplyDelayMilliseconds })}
                />

                <SettingsToggle
                  id={`friend-use-global-consecutive-delay-${selectedContact.friend.id}`}
                  label="沿用通用连发间隔"
                  description="关闭后，可以为这位好友单独设置一次回复中多条消息的间隔。"
                  checked={draft.useGlobalConsecutiveMessageDelay}
                  disabled={!draft.isEnabled}
                  onCheckedChange={(checked) => updateDraft({ useGlobalConsecutiveMessageDelay: checked })}
                />

                <ReplyTimingFields
                  idPrefix={`friend-consecutive-timing-${selectedContact.friend.id}`}
                  title="同次多条消息间隔"
                  description="只作用于这位好友一次回复拆成的第二条及后续消息。"
                  mode={draft.consecutiveMessageDelayMode}
                  fixedDelayMilliseconds={draft.fixedConsecutiveMessageDelayMilliseconds}
                  minimumDelayMilliseconds={draft.minimumConsecutiveMessageDelayMilliseconds}
                  maximumDelayMilliseconds={draft.maximumConsecutiveMessageDelayMilliseconds}
                  disabled={!draft.isEnabled || draft.useGlobalConsecutiveMessageDelay}
                  onModeChange={(consecutiveMessageDelayMode) => updateDraft({ consecutiveMessageDelayMode })}
                  onFixedDelayChange={(fixedConsecutiveMessageDelayMilliseconds) => updateDraft({ fixedConsecutiveMessageDelayMilliseconds })}
                  onMinimumDelayChange={(minimumConsecutiveMessageDelayMilliseconds) => updateDraft({ minimumConsecutiveMessageDelayMilliseconds })}
                  onMaximumDelayChange={(maximumConsecutiveMessageDelayMilliseconds) => updateDraft({ maximumConsecutiveMessageDelayMilliseconds })}
                />

                <SettingsToggle
                  id={`friend-use-global-reply-message-count-${selectedContact.friend.id}`}
                  label="沿用通用回复条数"
                  description="关闭后，可以为这位好友单独设置一次回复包含的消息条数范围。"
                  checked={draft.useGlobalReplyMessageCount}
                  disabled={!draft.isEnabled}
                  onCheckedChange={(checked) => updateDraft({ useGlobalReplyMessageCount: checked })}
                />

                <ReplyMessageCountFields
                  idPrefix={`friend-reply-message-count-${selectedContact.friend.id}`}
                  minimum={draft.minimumReplyMessageCount}
                  maximum={draft.maximumReplyMessageCount}
                  disabled={!draft.isEnabled || draft.useGlobalReplyMessageCount}
                  onMinimumChange={(minimumReplyMessageCount) => updateDraft({ minimumReplyMessageCount })}
                  onMaximumChange={(maximumReplyMessageCount) => updateDraft({ maximumReplyMessageCount })}
                />

                <SettingsToggle
                  id={`friend-use-global-question-policy-${selectedContact.friend.id}`}
                  label="沿用通用疑问节奏"
                  description="关闭后，可以为这位好友单独设置连续疑问轮次上限。"
                  checked={draft.useGlobalQuestionPolicy}
                  disabled={!draft.isEnabled}
                  onCheckedChange={(checked) => updateDraft({ useGlobalQuestionPolicy: checked })}
                />

                <SettingsNumberField
                  id={`friend-maximum-question-turns-${selectedContact.friend.id}`}
                  label="连续疑问轮次上限"
                  description="达到上限后，这位好友下一轮必须使用陈述语气收尾。"
                  value={draft.maximumConsecutiveQuestionTurns}
                  min={1}
                  suffix="轮"
                  disabled={!draft.isEnabled || draft.useGlobalQuestionPolicy}
                  onValueChange={(maximumConsecutiveQuestionTurns) => updateDraft({ maximumConsecutiveQuestionTurns })}
                />

                <SettingsToggle
                  id={`friend-private-chat-enabled-${selectedContact.friend.id}`}
                  label="允许主动发起私信"
                  description="允许这位好友主动向其他好友发起一对一会话。"
                  checked={draft.canInitiatePrivateChats}
                  disabled={!draft.isEnabled}
                  onCheckedChange={(checked) => updateDraft({ canInitiatePrivateChats: checked })}
                />

                <SettingsToggle
                  id={`friend-create-group-enabled-${selectedContact.friend.id}`}
                  label="允许主动创建群聊"
                  description="允许这位好友发起不包含本地用户的好友群聊。"
                  checked={draft.canInitiateGroupChats}
                  disabled={!draft.isEnabled}
                  onCheckedChange={(checked) => updateDraft({ canInitiateGroupChats: checked })}
                />

                <SettingsToggle
                  id={`friend-join-group-enabled-${selectedContact.friend.id}`}
                  label="允许加入好友群聊"
                  description="允许这位好友被其他好友邀请加入群聊。"
                  checked={draft.canJoinGroupChats}
                  disabled={!draft.isEnabled}
                  onCheckedChange={(checked) => updateDraft({ canJoinGroupChats: checked })}
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

            <aside className="overflow-hidden rounded-xl border border-border bg-surface" aria-label={`${selectedContact.friend.nickname}的当前设置状态`}>
              <div className="border-b border-border px-5 py-4">
                <h3 className="text-sm font-semibold text-foreground">当前状态</h3>
              </div>
              <dl className="divide-y divide-border px-5">
                <StatusRow
                  icon={Server}
                  label="AI 模型"
                  value={modelSettings.data?.effectiveModel || '未配置'}
                />
                <StatusRow
                  icon={CircleGauge}
                  label="参与状态"
                  value={draft.isEnabled ? '参与互动' : '暂停参与'}
                  tone={draft.isEnabled ? 'success' : 'muted'}
                />
                <StatusRow
                  icon={MessageCircleMore}
                  label="主动程度"
                  value={getLevelLabel(draft.initiativeLevel)}
                />
                <StatusRow
                  icon={UsersRound}
                  label="互动权限"
                  value={getFriendScopeLabel(draft)}
                />
                <StatusRow
                  icon={TimerReset}
                  label="回复间隔"
                  value={draft.useGlobalReplyDelay
                    ? '沿用通用设置'
                    : formatReplyTiming(draft)}
                />
              </dl>
            </aside>
          </div>
        )}
      </div>
    </div>
  )
}

function FriendSettingsLoadingState() {
  return (
    <div className="grid min-h-[540px] overflow-hidden rounded-xl border border-border bg-surface lg:grid-cols-[270px_minmax(0,1fr)]" role="status" aria-label="正在加载好友列表">
      <div className="border-r border-border p-4">
        <Skeleton className="h-9 w-full rounded-lg" />
        <div className="mt-4 grid gap-3">
          <Skeleton className="h-13 rounded-lg" />
          <Skeleton className="h-13 rounded-lg" />
          <Skeleton className="h-13 rounded-lg" />
        </div>
      </div>
      <div className="bg-surface-muted p-5">
        <FriendDetailLoadingState />
      </div>
    </div>
  )
}

function FriendDetailLoadingState() {
  return (
    <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_230px]" role="status" aria-label="正在加载好友专有设置">
      <Skeleton className="h-[510px] rounded-xl" />
      <Skeleton className="h-64 rounded-xl" />
    </div>
  )
}

function areSettingsEqual(
  first: UpdateAiAccountAutonomySettingsRequest,
  second: AiAccountAutonomySettingsResponse,
) {
  return first.isEnabled === second.isEnabled
    && first.initiativeLevel === second.initiativeLevel
    && first.canInitiatePrivateChats === second.canInitiatePrivateChats
    && first.canInitiateGroupChats === second.canInitiateGroupChats
    && first.canJoinGroupChats === second.canJoinGroupChats
    && first.useGlobalReplyDelay === second.useGlobalReplyDelay
    && first.replyDelayMode === second.replyDelayMode
    && first.fixedReplyDelayMilliseconds === second.fixedReplyDelayMilliseconds
    && first.minimumReplyDelayMilliseconds === second.minimumReplyDelayMilliseconds
    && first.maximumReplyDelayMilliseconds === second.maximumReplyDelayMilliseconds
    && first.useGlobalConsecutiveMessageDelay === second.useGlobalConsecutiveMessageDelay
    && first.consecutiveMessageDelayMode === second.consecutiveMessageDelayMode
    && first.fixedConsecutiveMessageDelayMilliseconds === second.fixedConsecutiveMessageDelayMilliseconds
    && first.minimumConsecutiveMessageDelayMilliseconds === second.minimumConsecutiveMessageDelayMilliseconds
    && first.maximumConsecutiveMessageDelayMilliseconds === second.maximumConsecutiveMessageDelayMilliseconds
    && first.useGlobalQuestionPolicy === second.useGlobalQuestionPolicy
    && first.maximumConsecutiveQuestionTurns === second.maximumConsecutiveQuestionTurns
    && first.useGlobalReplyMessageCount === second.useGlobalReplyMessageCount
    && first.minimumReplyMessageCount === second.minimumReplyMessageCount
    && first.maximumReplyMessageCount === second.maximumReplyMessageCount
}

function areModelSettingsEqual(
  first: UpdateAiAccountModelConnectionSettingsRequest,
  second: AiAccountModelConnectionSettingsResponse,
) {
  return first.useGlobalSettings === second.useGlobalSettings
    && first.baseUrl === second.baseUrl
    && first.model === second.model
    && first.apiKey.length === 0
    && !first.clearApiKey
}

function toUpdateRequest(
  settings: AiAccountAutonomySettingsResponse,
): UpdateAiAccountAutonomySettingsRequest {
  return {
    isEnabled: settings.isEnabled,
    initiativeLevel: settings.initiativeLevel,
    canInitiatePrivateChats: settings.canInitiatePrivateChats,
    canInitiateGroupChats: settings.canInitiateGroupChats,
    canJoinGroupChats: settings.canJoinGroupChats,
    useGlobalReplyDelay: settings.useGlobalReplyDelay,
    replyDelayMode: settings.replyDelayMode,
    fixedReplyDelayMilliseconds: settings.fixedReplyDelayMilliseconds,
    minimumReplyDelayMilliseconds: settings.minimumReplyDelayMilliseconds,
    maximumReplyDelayMilliseconds: settings.maximumReplyDelayMilliseconds,
    useGlobalConsecutiveMessageDelay: settings.useGlobalConsecutiveMessageDelay,
    consecutiveMessageDelayMode: settings.consecutiveMessageDelayMode,
    fixedConsecutiveMessageDelayMilliseconds: settings.fixedConsecutiveMessageDelayMilliseconds,
    minimumConsecutiveMessageDelayMilliseconds: settings.minimumConsecutiveMessageDelayMilliseconds,
    maximumConsecutiveMessageDelayMilliseconds: settings.maximumConsecutiveMessageDelayMilliseconds,
    useGlobalQuestionPolicy: settings.useGlobalQuestionPolicy,
    maximumConsecutiveQuestionTurns: settings.maximumConsecutiveQuestionTurns,
    useGlobalReplyMessageCount: settings.useGlobalReplyMessageCount,
    minimumReplyMessageCount: settings.minimumReplyMessageCount,
    maximumReplyMessageCount: settings.maximumReplyMessageCount,
  }
}

function toModelUpdateRequest(
  settings: AiAccountModelConnectionSettingsResponse,
): UpdateAiAccountModelConnectionSettingsRequest {
  return {
    useGlobalSettings: settings.useGlobalSettings,
    baseUrl: settings.baseUrl,
    model: settings.model,
    apiKey: '',
    clearApiKey: false,
  }
}

function getFriendScopeLabel(settings: UpdateAiAccountAutonomySettingsRequest) {
  if (!settings.isEnabled) return '暂不参与'

  const enabledCount = [
    settings.canInitiatePrivateChats,
    settings.canInitiateGroupChats,
    settings.canJoinGroupChats,
  ].filter(Boolean).length

  if (enabledCount === 3) return '全部允许'
  if (enabledCount === 0) return '均未允许'
  return `已允许 ${enabledCount} 项`
}
