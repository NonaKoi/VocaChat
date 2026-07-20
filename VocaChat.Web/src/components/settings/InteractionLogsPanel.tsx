import { AlertTriangle, CheckCircle2, RefreshCw } from 'lucide-react'
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
            记录回复生成和表达规则校验中的问题，最多保留最近 500 条。
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
          title="暂无互动问题"
          description="生成或保存发生异常时，会在这里留下简明记录。"
        />
      ) : (
        <ol className="divide-y divide-border">
          {logs.data.map((log) => (
            <li key={log.id} className="grid gap-2 px-5 py-4 sm:grid-cols-[170px_minmax(0,1fr)]">
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
                <div className="flex items-center gap-2">
                  <AlertTriangle
                    className={log.severity === 'Error'
                      ? 'size-4 shrink-0 text-destructive'
                      : 'size-4 shrink-0 text-primary'}
                    aria-hidden="true"
                  />
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
          ))}
        </ol>
      )}
    </section>
  )
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
