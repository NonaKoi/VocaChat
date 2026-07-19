import { useEffect, useMemo, useRef, useState } from 'react'
import {
  ArrowRight,
  CheckCircle2,
  MessagesSquare,
  RefreshCw,
  UsersRound,
  XCircle,
} from 'lucide-react'
import type {
  AutonomousGroupChatDecisionResponse,
  AutonomousGroupChatDecisionStage,
  AutonomousGroupChatExecutionResponse,
  ContactResponse,
} from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { EmptyState } from '@/components/feedback/EmptyState'
import { ErrorState } from '@/components/feedback/ErrorState'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useAutonomousGroupChat } from '@/hooks/useAutonomousGroupChat'
import { cn } from '@/lib/utils'
import type { RemoteStatus } from '@/types/remoteStatus'

interface AutonomousGroupChatPanelProps {
  contacts: ContactResponse[]
  contactStatus: RemoteStatus
  contactErrorMessage?: string
  onReloadContacts: () => void | Promise<void>
  onOpenGroupChat?: (groupChatId: string) => void | Promise<void>
}

const stageDescriptions: Record<AutonomousGroupChatDecisionStage, string> = {
  Approved: '当前权限、关系和人数条件均满足，可以开始一次好友群聊。',
  TooFewParticipants: '需要至少选择三位好友。',
  TooManyParticipants: '选择人数超过了通用设置中的单次上限。',
  DuplicateParticipant: '参与者列表中存在重复好友。',
  AccountNotFound: '至少一位已选择的好友已经不存在。',
  GlobalDisabled: '通用设置没有启用好友自主互动。',
  GroupChatsDisabled: '通用设置不允许好友自主组成群聊。',
  ParticipantDisabled: '至少一位好友关闭了自主互动。',
  ParticipantCannotJoin: '至少一位好友不允许加入自主群聊。',
  NoEligibleInitiator: '当前没有好友允许主动发起群聊。',
  ScoreBelowThreshold: '本次成员关系分没有达到当前频率的启动门槛。',
}

/** 提供一次可解释、由用户明确触发的自主好友群聊验收入口。 */
export function AutonomousGroupChatPanel({
  contacts,
  contactStatus,
  contactErrorMessage,
  onReloadContacts,
  onOpenGroupChat,
}: AutonomousGroupChatPanelProps) {
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [topic, setTopic] = useState('')
  const previewButtonRef = useRef<HTMLButtonElement>(null)
  const executionButtonRef = useRef<HTMLButtonElement>(null)
  const interaction = useAutonomousGroupChat(selectedIds)
  const selectedContacts = useMemo(
    () => contacts.filter((contact) => selectedIds.includes(contact.friend.id)),
    [contacts, selectedIds],
  )
  const isBusy = interaction.previewStatus === 'loading'
    || interaction.executionStatus === 'loading'
  const canSubmit = selectedIds.length >= 3 && !isBusy

  useEffect(() => {
    if (
      interaction.previewStatus === 'success'
      || interaction.previewStatus === 'error'
    ) {
      previewButtonRef.current?.focus()
    }
  }, [interaction.previewStatus])

  useEffect(() => {
    if (
      interaction.executionStatus === 'success'
      || interaction.executionStatus === 'error'
    ) {
      executionButtonRef.current?.focus()
    }
  }, [interaction.executionStatus])

  function toggleParticipant(accountId: string) {
    setSelectedIds((current) => current.includes(accountId)
      ? current.filter((id) => id !== accountId)
      : [...current, accountId])
  }

  async function handlePreview() {
    await interaction.evaluate()
  }

  async function handleExecution() {
    await interaction.execute(topic)
  }

  if (contactStatus === 'loading' || contactStatus === 'idle') {
    return (
      <div className="grid gap-4" role="status" aria-label="正在加载好友群聊设置">
        <Skeleton className="h-48 rounded-xl" />
        <Skeleton className="h-64 rounded-xl" />
      </div>
    )
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

  if (contacts.length < 3) {
    return (
      <div className="min-h-96 overflow-hidden rounded-xl border border-border bg-surface">
        <EmptyState
          icon={UsersRound}
          title="需要至少三位好友"
          description="好友群聊只由好友参与，请先添加足够的好友。"
        />
      </div>
    )
  }

  return (
    <section
      className="overflow-hidden rounded-xl border border-border bg-surface"
      aria-labelledby="autonomous-group-chat-title"
    >
      <header className="flex flex-wrap items-start justify-between gap-4 border-b border-border px-5 py-4">
        <div>
          <div className="flex items-center gap-2">
            <UsersRound className="size-[18px] text-primary" strokeWidth={1.8} aria-hidden="true" />
            <h3 id="autonomous-group-chat-title" className="text-base font-semibold text-foreground">
              好友群聊测试
            </h3>
          </div>
          <p className="mt-1 max-w-2xl text-xs leading-5 text-muted-foreground">
            选择至少三位好友。预览只判断，执行时才会创建或复用好友群聊并保存正式消息。
          </p>
        </div>
        <p className="rounded-md bg-surface-muted px-2.5 py-1 text-xs font-medium tabular-nums text-foreground">
          已选择 {selectedIds.length} 位
        </p>
      </header>

      <fieldset className="border-0 p-5">
        <legend className="text-sm font-semibold text-foreground">选择参与好友</legend>
        <div className="mt-3 grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
          {contacts.map((contact) => {
            const checked = selectedIds.includes(contact.friend.id)
            return (
              <label
                key={contact.id}
                className={cn(
                  'flex min-w-0 items-center gap-3 rounded-lg border px-3 py-2.5 transition-colors',
                  checked
                    ? 'border-primary/35 bg-primary-soft'
                    : 'border-border bg-surface hover:bg-surface-muted',
                  isBusy && 'cursor-not-allowed opacity-65',
                )}
              >
                <input
                  type="checkbox"
                  name="autonomousGroupChatParticipants"
                  value={contact.friend.id}
                  checked={checked}
                  disabled={isBusy}
                  onChange={() => toggleParticipant(contact.friend.id)}
                  className="size-4 shrink-0 accent-primary focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                />
                <EntityAvatar
                  name={contact.friend.nickname}
                  src={contact.friend.avatarUrl}
                  size="small"
                />
                <span className="min-w-0">
                  <span className="block truncate text-sm font-medium text-foreground">
                    {contact.friend.nickname}
                  </span>
                  <span className="mt-0.5 block truncate text-xs text-muted-foreground">
                    VC号 {contact.friend.vcNumber}
                  </span>
                </span>
              </label>
            )
          })}
        </div>
      </fieldset>

      <div className="border-t border-border px-5 py-4">
        <label htmlFor="autonomous-group-chat-topic" className="text-sm font-medium text-foreground">
          本次话题 <span className="text-xs font-normal text-muted-foreground">选填</span>
        </label>
        <input
          id="autonomous-group-chat-topic"
          name="autonomousGroupChatTopic"
          type="text"
          autoComplete="off"
          maxLength={200}
          value={topic}
          disabled={isBusy}
          onChange={(event) => setTopic(event.target.value)}
          placeholder="例如：周末准备去哪里；留空时从共同兴趣中选择…"
          className="form-control mt-2"
        />
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
          <p className="text-xs leading-5 text-muted-foreground">
            {selectedIds.length < 3
              ? `还需要选择 ${3 - selectedIds.length} 位好友。`
              : `${selectedContacts.map((contact) => contact.friend.nickname).join('、')} 将参与本次判断。`}
          </p>
          <div className="flex flex-wrap gap-2">
            <Button
              ref={previewButtonRef}
              variant="outline"
              disabled={!canSubmit}
              aria-busy={interaction.previewStatus === 'loading'}
              onClick={() => void handlePreview()}
            >
              <RefreshCw
                className={cn('size-4', interaction.previewStatus === 'loading' && 'animate-spin')}
                aria-hidden="true"
              />
              {interaction.previewStatus === 'loading' ? '正在判断…' : '预览群聊判断'}
            </Button>
            <Button
              ref={executionButtonRef}
              disabled={!canSubmit}
              aria-busy={interaction.executionStatus === 'loading'}
              onClick={() => void handleExecution()}
            >
              <MessagesSquare className="size-4" aria-hidden="true" />
              {interaction.executionStatus === 'loading' ? '正在交流…' : '尝试发起一次群聊'}
            </Button>
          </div>
        </div>
      </div>

      <div className="border-t border-border px-5 py-4" aria-live="polite">
        {interaction.errorMessage && (
          <p role="alert" className="rounded-lg border border-destructive/20 bg-danger-soft px-3 py-2 text-sm text-destructive">
            {interaction.errorMessage}
          </p>
        )}
        {!interaction.errorMessage && !interaction.decision && (
          <p className="text-xs leading-5 text-muted-foreground">
            判断结果会显示发起者、关系分和人数上限；执行结果会显示本次实际保存的群消息。
          </p>
        )}
        {interaction.decision && (
          <div className="grid gap-4">
            <DecisionResult decision={interaction.decision} contacts={contacts} />
            {interaction.execution && (
              <ExecutionResult
                result={interaction.execution}
                onOpenGroupChat={onOpenGroupChat}
              />
            )}
          </div>
        )}
      </div>
    </section>
  )
}

function DecisionResult({
  decision,
  contacts,
}: {
  decision: AutonomousGroupChatDecisionResponse
  contacts: ContactResponse[]
}) {
  const ResultIcon = decision.isApproved ? CheckCircle2 : XCircle
  const initiatorName = contacts.find(
    (contact) => contact.friend.id === decision.initiatorAiAccountId,
  )?.friend.nickname

  return (
    <div className="grid gap-3">
      <div className="flex items-start gap-3">
        <span className={cn(
          'mt-0.5 grid size-8 shrink-0 place-items-center rounded-lg',
          decision.isApproved
            ? 'bg-success/10 text-success'
            : 'bg-surface-muted text-muted-foreground',
        )}>
          <ResultIcon className="size-4" aria-hidden="true" />
        </span>
        <div className="min-w-0">
          <p className={cn('text-sm font-semibold', decision.isApproved ? 'text-success' : 'text-foreground')}>
            {decision.isApproved ? '可以开始好友群聊' : '本次不发起'}
          </p>
          <p className="mt-1 text-xs leading-5 text-muted-foreground">
            {stageDescriptions[decision.stage]}
          </p>
          {initiatorName && (
            <p className="mt-1 text-xs text-foreground">
              判断发起者：<span className="font-semibold">{initiatorName}</span>
            </p>
          )}
        </div>
      </div>
      <dl className="grid divide-y divide-border border-y border-border sm:grid-cols-4 sm:divide-x sm:divide-y-0">
        <ScoreItem label="平均关系" value={decision.averageRelationshipScore} />
        <ScoreItem label="最弱关系" value={decision.weakestRelationshipScore} />
        <ScoreItem label="共同兴趣" value={decision.sharedInterestBonus} signed />
        <ScoreItem label="最终 / 门槛" value={decision.finalScore} suffix={` / ${formatScore(decision.threshold)}`} />
      </dl>
      <p className="text-[11px] leading-5 text-muted-foreground">
        当前最多允许 {decision.maximumMembers} 位好友参与；预览不会创建群聊或消息。
      </p>
    </div>
  )
}

function ExecutionResult({
  result,
  onOpenGroupChat,
}: {
  result: AutonomousGroupChatExecutionResponse
  onOpenGroupChat?: (groupChatId: string) => void | Promise<void>
}) {
  const messagesWereSaved = result.messages.length > 0
  const groupChatId = result.groupChat?.id

  return (
    <div
      className={cn(
        'rounded-lg border px-4 py-3',
        result.status === 'Completed'
          ? 'border-success/20 bg-success/5'
          : 'border-destructive/20 bg-danger-soft',
      )}
      role={result.status === 'Completed' ? 'status' : 'alert'}
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-sm font-semibold text-foreground">
            {result.status === 'Completed'
              ? `${result.messages.length} 条群消息已经保存`
              : messagesWereSaved
                ? `已有 ${result.messages.length} 条消息保存，后续流程未完成`
                : '本次好友群聊没有产生消息'}
          </p>
          {result.errorMessage && (
            <p className="mt-1 text-xs leading-5 text-destructive">{result.errorMessage}</p>
          )}
        </div>
        {groupChatId && messagesWereSaved && onOpenGroupChat && (
          <Button variant="outline" onClick={() => void onOpenGroupChat(groupChatId)}>
            查看好友群聊
            <ArrowRight className="size-4" aria-hidden="true" />
          </Button>
        )}
      </div>
      {result.session && (
        <dl className="mt-3 flex flex-wrap gap-x-5 gap-y-1 border-t border-border/70 pt-3 text-xs leading-5">
          <div className="flex gap-1.5">
            <dt className="text-muted-foreground">话题</dt>
            <dd className="max-w-64 truncate text-foreground">{result.session.topic}</dd>
          </div>
          <div className="flex gap-1.5">
            <dt className="text-muted-foreground">参与者</dt>
            <dd className="tabular-nums text-foreground">{result.session.participantAiAccountIds.length} 位</dd>
          </div>
          <div className="flex gap-1.5">
            <dt className="text-muted-foreground">群聊</dt>
            <dd className="text-foreground">{result.groupChatCreated ? '本次新建' : '复用已有群聊'}</dd>
          </div>
        </dl>
      )}
      {messagesWereSaved && (
        <div className="mt-3 grid gap-2 text-xs leading-5">
          {result.messages.map((message) => (
            <p key={message.id} className="break-words rounded-md bg-surface px-3 py-2 text-foreground">
              <span className="font-semibold">{message.senderDisplayName}：</span>
              {message.content}
            </p>
          ))}
        </div>
      )}
    </div>
  )
}

function ScoreItem({
  label,
  value,
  signed = false,
  suffix = '',
}: {
  label: string
  value: number
  signed?: boolean
  suffix?: string
}) {
  return (
    <div className="px-3 py-2.5 first:pl-0 last:pr-0 sm:first:pl-3 sm:last:pr-3">
      <dt className="text-[11px] text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm font-semibold tabular-nums text-foreground">
        {signed && value > 0 ? '+' : ''}{formatScore(value)}{suffix}
      </dd>
    </div>
  )
}

function formatScore(value: number) {
  return Number.isInteger(value) ? value.toString() : value.toFixed(1)
}
