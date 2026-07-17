import { useMemo, useState } from 'react'
import { Plus, Search, SlidersHorizontal, UserRoundSearch } from 'lucide-react'
import type { AiAccountResponse } from '@/api/types'
import { ListItem } from '@/components/common/ListItem'
import { Panel } from '@/components/common/Panel'
import { EmptyState } from '@/components/feedback/EmptyState'
import { ErrorState } from '@/components/feedback/ErrorState'
import { LoadingState } from '@/components/feedback/LoadingState'
import { Button } from '@/components/ui/button'
import type { RemoteStatus } from '@/types/remoteStatus'

interface AiAccountListProps {
  accounts: AiAccountResponse[]
  status: RemoteStatus
  selectedId?: string
  isCreating: boolean
  errorMessage?: string
  onSelect: (id: string) => void
  onCreate: () => void
  onRetry: () => void
}

/** 将长期存在的 AI 账号以“好友”心智模型呈现给用户。 */
export function AiAccountList({
  accounts,
  status,
  selectedId,
  isCreating,
  errorMessage,
  onSelect,
  onCreate,
  onRetry,
}: AiAccountListProps) {
  const [searchText, setSearchText] = useState('')
  const normalizedSearchText = searchText.trim().toLocaleLowerCase()
  const filteredAccounts = useMemo(
    () =>
      accounts.filter((account) => {
        if (!normalizedSearchText) {
          return true
        }

        return [
          account.nickname,
          account.vcNumber,
          account.identityDescription,
          account.personality,
          account.speakingStyle,
          account.signature,
          account.location,
          account.occupation,
          account.hometown,
          ...account.interestTags,
          ...account.personalityTags,
        ].some((value) =>
          value.toLocaleLowerCase().includes(normalizedSearchText),
        )
      }),
    [accounts, normalizedSearchText],
  )

  return (
    <Panel aria-labelledby="friend-list-title">
      <header className="border-b border-border px-4 pt-4">
        <div className="flex items-center gap-2">
          <label className="relative min-w-0 flex-1">
            <span className="sr-only">搜索好友</span>
            <Search
              className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
              strokeWidth={1.8}
              aria-hidden="true"
            />
            <input
              type="search"
              name="friend-search"
              autoComplete="off"
              value={searchText}
              onChange={(event) => setSearchText(event.target.value)}
              placeholder="搜索好友"
              className="h-10 w-full rounded-lg border border-transparent bg-surface-muted pr-3 pl-9 text-sm text-foreground outline-none transition-colors placeholder:text-muted-foreground focus:border-primary/35 focus:bg-surface focus:ring-2 focus:ring-primary/12"
            />
          </label>
          <Button
            size="icon"
            variant="ghost"
            className="size-10 bg-surface-muted text-foreground hover:bg-primary-soft hover:text-primary"
            onClick={onCreate}
            disabled={status !== 'success'}
            aria-label="寻找新朋友"
            aria-pressed={isCreating}
            title="寻找新朋友"
          >
            <Plus className="size-[19px]" aria-hidden="true" />
          </Button>
        </div>

        <div className="mt-4 flex items-end justify-between gap-3">
          <div className="flex min-w-0 items-end gap-7">
            <h1
              id="friend-list-title"
              className="border-b-2 border-primary px-1 pb-3 text-sm font-semibold text-primary"
            >
              好友列表
              {status === 'success' && (
                <span className="ml-1 font-medium">({accounts.length})</span>
              )}
            </h1>
            <span className="pb-3 text-sm text-muted-foreground">默认分组</span>
          </div>
          <SlidersHorizontal
            className="mb-3 size-4 text-muted-foreground"
            strokeWidth={1.8}
            aria-hidden="true"
          />
        </div>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto px-3 py-4">
        {(status === 'idle' || status === 'loading') && (
          <LoadingState variant="list" />
        )}
        {status === 'error' && (
          <ErrorState message={errorMessage} onRetry={onRetry} />
        )}
        {status === 'success' && accounts.length === 0 && (
          <EmptyState
            icon={UserRoundSearch}
            title="还没有好友"
            description="寻找一位新朋友，之后就能邀请对方加入群聊。"
            compact
          />
        )}
        {status === 'success' &&
          accounts.length > 0 &&
          filteredAccounts.length === 0 && (
            <EmptyState
              icon={Search}
              title="没有匹配的好友"
              description="换一个昵称或资料关键词再试试。"
              compact
            />
          )}
        {status === 'success' && filteredAccounts.length > 0 && (
          <ul className="grid gap-1.5" aria-label="好友列表">
            {filteredAccounts.map((account) => (
              <li key={account.id}>
                <ListItem
                  title={account.nickname}
                  description={account.identityDescription || '暂未填写个人介绍'}
                  avatarUrl={account.avatarUrl}
                  selected={!isCreating && selectedId === account.id}
                  onSelect={() => onSelect(account.id)}
                />
              </li>
            ))}
          </ul>
        )}
      </div>

      <footer className="border-t border-border p-3">
        <Button
          variant="outline"
          className="w-full"
          onClick={onCreate}
          disabled={status !== 'success'}
        >
          <UserRoundSearch className="size-4" aria-hidden="true" />
          寻找新朋友
        </Button>
      </footer>
    </Panel>
  )
}
