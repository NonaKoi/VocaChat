import { CircleUserRound } from 'lucide-react'
import type { AiAccountResponse } from '@/api/types'
import { ListItem } from '@/components/common/ListItem'
import { Panel } from '@/components/common/Panel'
import { EmptyState } from '@/components/feedback/EmptyState'
import { ErrorState } from '@/components/feedback/ErrorState'
import { LoadingState } from '@/components/feedback/LoadingState'
import { Badge } from '@/components/ui/badge'
import type { RemoteStatus } from '@/types/remoteStatus'

interface AiAccountListProps {
  accounts: AiAccountResponse[]
  status: RemoteStatus
  selectedId?: string
  errorMessage?: string
  onSelect: (id: string) => void
  onRetry: () => void
}

export function AiAccountList({
  accounts,
  status,
  selectedId,
  errorMessage,
  onSelect,
  onRetry,
}: AiAccountListProps) {
  return (
    <Panel aria-labelledby="ai-account-list-title">
      <header className="flex min-h-28 items-center justify-between border-b border-border px-6 py-5">
        <div className="grid gap-1">
          <p className="text-xs font-medium tracking-[0.08em] text-muted-foreground uppercase">
            长期 AI 身份
          </p>
          <h1
            id="ai-account-list-title"
            className="font-display text-xl font-semibold tracking-[-0.01em]"
          >
            AI 账号
          </h1>
        </div>
        {status === 'success' && <Badge>{accounts.length}</Badge>}
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto">
        {(status === 'idle' || status === 'loading') && (
          <LoadingState variant="list" />
        )}
        {status === 'error' && (
          <ErrorState message={errorMessage} onRetry={onRetry} />
        )}
        {status === 'success' && accounts.length === 0 && (
          <EmptyState
            icon={CircleUserRound}
            title="还没有 AI 账号"
            description="创建的长期 AI 身份会显示在这里。"
            compact
          />
        )}
        {status === 'success' && accounts.length > 0 && (
          <ul className="grid gap-1.5 p-2" aria-label="AI 账号列表">
            {accounts.map((account) => (
              <li key={account.id}>
                <ListItem
                  title={account.nickname}
                  description={account.identityDescription || '未填写身份描述'}
                  selected={selectedId === account.id}
                  onSelect={() => onSelect(account.id)}
                />
              </li>
            ))}
          </ul>
        )}
      </div>

      <footer className="border-t border-border px-6 py-3 text-xs text-muted-foreground">
        账号资料保存在本地 VocaChat 数据库
      </footer>
    </Panel>
  )
}
