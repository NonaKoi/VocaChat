import { AlertTriangle, CheckCircle2, MessagesSquare, RefreshCw, Route } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import type { AiInteractionDiagnosticLogResponse } from '@/api/types'
import { ErrorState } from '@/components/feedback/ErrorState'
import { EmptyState } from '@/components/feedback/EmptyState'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useAiInteractionDiagnosticLogs } from '@/hooks/useAiInteractionDiagnosticLogs'

/** 将内部生成问题集中在设置页展示，避免技术错误打断聊天。 */
export function InteractionLogsPanel() {
  const logs = useAiInteractionDiagnosticLogs()

  if (logs.status === 'loading') {
    return (
      <div className="grid gap-3" role="status" aria-label="正在加载互动日志">
        <Skeleton className="h-28 rounded-xl" />
        <Skeleton className="h-28 rounded-xl" />
      </div>
    )
  }

  if (logs.status === 'error') {
    return <ErrorState message={logs.errorMessage} onRetry={logs.reload} />
  }

  return (
    <section className="overflow-hidden rounded-xl border border-border bg-surface" aria-labelledby="interaction-logs-title">
      <header className="flex items-start justify-between gap-4 border-b border-border px-5 py-4">
        <div>
          <h3 id="interaction-logs-title" className="text-base font-semibold text-foreground">
            互动日志
          </h3>
          <p className="mt-1 text-xs leading-5 text-muted-foreground">
            集中展示群聊发言安排、规则回退和回复异常，最多保留最近 500 条。
          </p>
        </div>
        <Button variant="outline" className="h-8 px-3 text-xs" onClick={() => void logs.reload()}>
          <RefreshCw className="size-4" aria-hidden="true" />
          刷新
        </Button>
      </header>

      {logs.data.length === 0 ? (
        <EmptyState
          icon={CheckCircle2}
          title="暂无互动记录"
          description="群聊产生发言安排或回复发生异常后，会在这里留下简明记录。"
        />
      ) : (
        <ol className="divide-y divide-border">
          {logs.data.map((log) => {
            const presentation = getDiagnosticPresentation(log)
            const Icon = presentation.icon

            return (
              <li
                key={log.id}
                className="grid gap-2 px-5 py-4 [contain-intrinsic-size:auto_96px] [content-visibility:auto] sm:grid-cols-[170px_minmax(0,1fr)]"
              >
                <div className="text-xs leading-5 text-muted-foreground">
                  <time dateTime={log.occurredAt}>
                    {new Intl.DateTimeFormat('zh-CN', {
                      dateStyle: 'medium',
                      timeStyle: 'medium',
                    }).format(new Date(log.occurredAt))}
                  </time>
                  <p className="mt-1">{getScenarioLabel(log.scenario)}</p>
                </div>
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <Icon
                      className={presentation.iconClassName}
                      aria-hidden="true"
                    />
                    <span className="rounded-md bg-surface-muted px-2 py-0.5 text-[11px] font-medium text-muted-foreground">
                      {presentation.label}
                    </span>
                    <p className="text-sm font-medium text-foreground">{log.summary}</p>
                    {log.wasRecovered && (
                      <span className="rounded-full bg-primary-soft px-2 py-0.5 text-[11px] text-primary">
                        已恢复
                      </span>
                    )}
                  </div>
                  <p className="mt-1 break-words text-xs leading-5 text-muted-foreground">
                    {log.detail}
                  </p>
                </div>
              </li>
            )
          })}
        </ol>
      )}
    </section>
  )
}

function getDiagnosticPresentation(
  log: AiInteractionDiagnosticLogResponse,
): {
  icon: LucideIcon
  iconClassName: string
  label: string
} {
  const labels: Record<AiInteractionDiagnosticLogResponse['code'], string> = {
    MessageGenerationFailed: '回复生成',
    MessagePersistenceFailed: '消息保存',
    ReplyTimingFailed: '回复调度',
    SelfMemoryDecision: '记忆更新',
    SelfMemoryPersistenceFailed: '记忆保存',
    GroupConversationPlanCreated: '群聊安排',
    GroupConversationPlanFallback: '规则接管',
    GroupConversationExecutionFailed: '群聊执行',
  }

  if (log.code === 'GroupConversationPlanCreated') {
    return {
      icon: Route,
      iconClassName: 'size-4 shrink-0 text-primary',
      label: labels[log.code],
    }
  }

  if (log.code === 'GroupConversationPlanFallback') {
    return {
      icon: MessagesSquare,
      iconClassName: 'size-4 shrink-0 text-primary',
      label: labels[log.code],
    }
  }

  return {
    icon: AlertTriangle,
    iconClassName: log.severity === 'Error'
      ? 'size-4 shrink-0 text-destructive'
      : 'size-4 shrink-0 text-primary',
    label: labels[log.code],
  }
}

function getScenarioLabel(scenario: string): string {
  const labels: Record<string, string> = {
    UserPrivateChat: '我的私信',
    GroupPrimaryReply: '我的群聊 · 首位回复',
    GroupFollowUpReply: '我的群聊 · 后续回复',
    AutonomousPrivateChat: '好友私信',
    AutonomousPrivateChatClosing: '好友私信 · 收束',
    AutonomousGroupChat: '好友群聊',
  }

  return labels[scenario] ?? scenario
}
