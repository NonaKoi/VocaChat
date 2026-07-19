import { useEffect, useState } from 'react'
import { ArrowRight, CheckCircle2, Gauge, MessageCircleMore, RefreshCw, XCircle } from 'lucide-react'
import type {
  AutonomousPrivateChatExecutionResponse,
  AutonomousPrivateChatDecisionResponse,
  AutonomousPrivateChatDecisionStage,
  AutonomousPrivateChatSessionEndReason,
  ContactResponse,
} from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { Button } from '@/components/ui/button'
import { useAutonomousPrivateChatExecution } from '@/hooks/useAutonomousPrivateChatExecution'
import { useAutonomousPrivateChatPreview } from '@/hooks/useAutonomousPrivateChatPreview'
import { cn } from '@/lib/utils'

interface PrivateChatDecisionPreviewProps {
  firstContact: ContactResponse
  secondContact: ContactResponse
  hasUnsavedRelationship: boolean
  onOpenPrivateChat?: (privateChatId: string) => void | Promise<void>
}

const stageDescriptions: Record<AutonomousPrivateChatDecisionStage, string> = {
  Approved: '当前权限、关系分数与冷却条件均满足，可以发起自主私信。',
  SelfInteractionNotAllowed: '好友不能与自己发起自主私信。',
  AccountNotFound: '其中一位好友已经不存在。',
  GlobalDisabled: '通用设置没有启用好友自主互动。',
  PrivateChatsDisabled: '通用设置不允许自主私信。',
  ParticipantDisabled: '至少一位好友关闭了自己的自主互动。',
  NoEligibleInitiator: '双方当前都没有主动发起私信的权限。',
  CooldownActive: '双方距离上次互动时间太近，仍处于冷却期。',
  ScoreBelowThreshold: '本次关系分数没有达到当前互动频率的发起门槛。',
}

const sessionEndReasonDescriptions: Record<AutonomousPrivateChatSessionEndReason, string> = {
  NaturalConclusion: '自然结束',
  PlannedLimitReached: '达到本次计划轮数',
  HardLimitReached: '达到系统轮数上限',
  ParticipantUnavailable: '参与者当前不可用',
  InteractionDisabled: '自主互动已关闭',
  GenerationFailed: '消息生成失败',
  MessagePersistenceFailed: '消息保存失败',
  RelationshipUpdateFailed: '互动记录更新失败',
  CancelledByUser: '已由用户取消',
  ContinuationProbabilityDeclined: '下一轮概率未通过',
}

/** 展示一次可解释的自主私信判断；该操作只读数据，不会真正发送消息。 */
export function PrivateChatDecisionPreview({
  firstContact,
  secondContact,
  hasUnsavedRelationship,
  onOpenPrivateChat,
}: PrivateChatDecisionPreviewProps) {
  const preview = useAutonomousPrivateChatPreview(
    firstContact.friend.id,
    secondContact.friend.id,
  )
  const execution = useAutonomousPrivateChatExecution(
    firstContact.friend.id,
    secondContact.friend.id,
  )
  const [topic, setTopic] = useState('')
  const decision = execution.data?.decision ?? preview.data

  useEffect(() => {
    setTopic('')
  }, [firstContact.friend.id, secondContact.friend.id])

  const initiator = decision?.initiatorAiAccountId === firstContact.friend.id
    ? firstContact.friend
    : decision?.initiatorAiAccountId === secondContact.friend.id
      ? secondContact.friend
      : undefined

  return (
    <section
      className="overflow-hidden rounded-xl border border-border bg-surface xl:col-span-2"
      aria-labelledby="private-chat-decision-title"
    >
      <div className="flex flex-wrap items-start justify-between gap-4 border-b border-border px-5 py-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <Gauge className="size-4 text-primary" strokeWidth={1.8} aria-hidden="true" />
            <h3 id="private-chat-decision-title" className="text-sm font-semibold text-foreground">
              自主私信判断
            </h3>
          </div>
          <p className="mt-1 text-xs leading-5 text-muted-foreground">
            读取已保存的互动设置与双向关系，预览当前是否适合发起私信。
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button
            variant="outline"
            disabled={preview.status === 'loading' || execution.status === 'loading' || hasUnsavedRelationship}
            aria-busy={preview.status === 'loading'}
            onClick={() => void preview.evaluate()}
          >
            <RefreshCw
              className={cn('size-4', preview.status === 'loading' && 'animate-spin')}
              aria-hidden="true"
            />
            {preview.status === 'loading'
              ? '正在判断'
              : decision
                ? '重新判断'
                : '预览私信判断'}
          </Button>
          <Button
            disabled={execution.status === 'loading' || preview.status === 'loading' || hasUnsavedRelationship}
            aria-busy={execution.status === 'loading'}
            onClick={() => void execution.execute(topic)}
          >
            <MessageCircleMore className="size-4" aria-hidden="true" />
            {execution.status === 'loading' ? '正在尝试' : '尝试发起一次私信'}
          </Button>
        </div>
      </div>

      <div className="px-5 py-4" aria-live="polite">
        <label className="mb-4 block" htmlFor="autonomous-private-chat-topic">
          <span className="text-xs font-medium text-foreground">本次话题</span>
          <span className="ml-2 text-[11px] text-muted-foreground">选填</span>
          <input
            id="autonomous-private-chat-topic"
            name="autonomousPrivateChatTopic"
            type="text"
            autoComplete="off"
            value={topic}
            maxLength={200}
            disabled={execution.status === 'loading' || hasUnsavedRelationship}
            onChange={(event) => setTopic(event.target.value)}
            placeholder="例如：最近看的电影；留空时由系统从双方兴趣中选择…"
            className="mt-2 h-10 w-full rounded-lg border border-border bg-surface-muted px-3 text-sm text-foreground outline-none transition-colors placeholder:text-muted-foreground focus:border-primary/50 focus:ring-2 focus:ring-ring/30 disabled:cursor-not-allowed disabled:opacity-60"
          />
        </label>
        {hasUnsavedRelationship && (
          <p className="rounded-lg border border-border bg-surface-muted px-3 py-2 text-xs leading-5 text-muted-foreground">
            请先保存当前关系，判断器才会读取到最新数值。
          </p>
        )}

        {!hasUnsavedRelationship && preview.status === 'idle' && execution.status === 'idle' && (
          <div className="flex min-w-0 items-center gap-3 text-sm text-muted-foreground">
            <div className="flex shrink-0 items-center gap-1.5" aria-hidden="true">
              <EntityAvatar name={firstContact.friend.nickname} src={firstContact.friend.avatarUrl} size="small" />
              <ArrowRight className="size-4" />
              <EntityAvatar name={secondContact.friend.nickname} src={secondContact.friend.avatarUrl} size="small" />
            </div>
            <p className="min-w-0 leading-6">
              预览只执行判断，不会创建会话、发送消息或修改关系数据。
            </p>
          </div>
        )}

        {!hasUnsavedRelationship && preview.status === 'error' && (
          <p role="alert" className="rounded-lg border border-destructive/20 bg-danger-soft px-3 py-2 text-sm text-destructive">
            {preview.errorMessage}
          </p>
        )}

        {!hasUnsavedRelationship && execution.status === 'error' && (
          <p role="alert" className="rounded-lg border border-destructive/20 bg-danger-soft px-3 py-2 text-sm text-destructive">
            {execution.errorMessage}
          </p>
        )}

        {!hasUnsavedRelationship && decision && (
          <div className="grid gap-4">
            <DecisionResult decision={decision} initiatorName={initiator?.nickname} />
            {execution.status === 'success' && execution.data && (
              <ExecutionResult
                result={execution.data}
                onOpenPrivateChat={onOpenPrivateChat}
              />
            )}
          </div>
        )}
      </div>
    </section>
  )
}

function ExecutionResult({
  result,
  onOpenPrivateChat,
}: {
  result: AutonomousPrivateChatExecutionResponse
  onOpenPrivateChat?: (privateChatId: string) => void | Promise<void>
}) {
  if (result.status === 'DecisionRejected') {
    return (
      <p className="rounded-lg border border-border bg-surface-muted px-3 py-2 text-xs leading-5 text-muted-foreground">
        本次判断没有通过，因此没有创建会话、发送消息或更新互动记录。
      </p>
    )
  }

  const privateChatId = result.privateChat?.id
  const messagesWereSaved = result.messages.length > 0

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
        <div className="min-w-0">
          <p className="text-sm font-semibold text-foreground">
            {result.status === 'Completed'
              ? `好友已完成 ${result.session?.completedRounds ?? 0} 轮私信交流`
              : messagesWereSaved
                ? `已有 ${result.messages.length} 条消息保存，后续流程未完成`
                : '本次私信没有完整保存'}
          </p>
          {result.errorMessage && (
            <p className="mt-1 text-xs leading-5 text-destructive">
              {result.errorMessage}
            </p>
          )}
        </div>
        {privateChatId && messagesWereSaved && onOpenPrivateChat && (
          <Button
            variant="outline"
            onClick={() => void onOpenPrivateChat(privateChatId)}
          >
            查看好友私信
            <ArrowRight className="size-4" aria-hidden="true" />
          </Button>
        )}
      </div>

      {result.session && (
        <dl className="mt-3 flex flex-wrap gap-x-5 gap-y-1 border-t border-border/70 pt-3 text-xs leading-5">
          <div className="flex min-w-0 gap-1.5">
            <dt className="text-muted-foreground">话题</dt>
            <dd className="max-w-64 truncate text-foreground">{result.session.topic}</dd>
          </div>
          <div className="flex gap-1.5">
            <dt className="text-muted-foreground">进度</dt>
            <dd className="tabular-nums text-foreground">
              已完成 {result.session.completedRounds} / {result.session.maximumRounds} 轮
            </dd>
          </div>
          <div className="flex gap-1.5">
            <dt className="text-muted-foreground">续聊比例</dt>
            <dd className="tabular-nums text-foreground">
              {result.session.continuationRatePercent}%
            </dd>
          </div>
          {result.session.endReason && (
            <div className="flex gap-1.5">
              <dt className="text-muted-foreground">结束原因</dt>
              <dd className="text-foreground">
                {sessionEndReasonDescriptions[result.session.endReason]}
              </dd>
            </div>
          )}
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

function DecisionResult({
  decision,
  initiatorName,
}: {
  decision: AutonomousPrivateChatDecisionResponse
  initiatorName?: string
}) {
  const ResultIcon = decision.isApproved ? CheckCircle2 : XCircle

  return (
    <div className="grid gap-4">
      <div className="flex items-start gap-3">
        <span
          className={cn(
            'mt-0.5 grid size-8 shrink-0 place-items-center rounded-lg',
            decision.isApproved
              ? 'bg-success/10 text-success'
              : 'bg-surface-muted text-muted-foreground',
          )}
        >
          <ResultIcon className="size-4" aria-hidden="true" />
        </span>
        <div className="min-w-0">
          <p className={cn('text-sm font-semibold', decision.isApproved ? 'text-success' : 'text-foreground')}>
            {decision.isApproved ? '可以发起' : '本次不发起'}
          </p>
          <p className="mt-1 text-xs leading-5 text-muted-foreground">
            {stageDescriptions[decision.stage]}
          </p>
          {initiatorName && (
            <p className="mt-1 text-xs text-foreground">
              判断发起方：<span className="font-semibold">{initiatorName}</span>
            </p>
          )}
          {decision.cooldownEndsAt && (
            <p className="mt-1 text-xs text-muted-foreground">
              冷却结束：{formatDateTime(decision.cooldownEndsAt)}
            </p>
          )}
        </div>
      </div>

      <dl className="grid divide-y divide-border border-y border-border sm:grid-cols-4 sm:divide-x sm:divide-y-0">
        <ScoreItem label="关系分" value={decision.relationshipScore} />
        <ScoreItem label="主动性" value={decision.initiativeAdjustment} signed />
        <ScoreItem label="随机扰动" value={decision.randomJitter} signed />
        <ScoreItem label="最终 / 门槛" value={decision.finalScore} suffix={` / ${formatScore(decision.threshold)}`} />
      </dl>

      <p className="text-[11px] leading-5 text-muted-foreground">
        随机扰动限制在 -10 至 +10，只影响临界判断，不会绕过权限和冷却规则。
      </p>
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
  const formattedValue = `${signed && value > 0 ? '+' : ''}${formatScore(value)}`

  return (
    <div className="px-3 py-2.5 first:pl-0 last:pr-0 sm:first:pl-3 sm:last:pr-3">
      <dt className="text-[11px] text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm font-semibold tabular-nums text-foreground">
        {formattedValue}{suffix}
      </dd>
    </div>
  )
}

function formatScore(value: number) {
  return Number.isInteger(value) ? value.toString() : value.toFixed(1)
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('zh-CN', {
    month: 'numeric',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}
