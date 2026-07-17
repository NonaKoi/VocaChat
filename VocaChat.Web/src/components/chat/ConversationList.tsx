import { useMemo, useState } from 'react'
import { MessageCircleMore, Plus, Search, UsersRound } from 'lucide-react'
import type { GroupChatResponse } from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { Panel } from '@/components/common/Panel'
import { EmptyState } from '@/components/feedback/EmptyState'
import { ErrorState } from '@/components/feedback/ErrorState'
import { LoadingState } from '@/components/feedback/LoadingState'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import type { RemoteStatus } from '@/types/remoteStatus'

interface ConversationListProps {
  groupChats: GroupChatResponse[]
  status: RemoteStatus
  selectedId?: string
  errorMessage?: string
  onSelect: (id: string) => void
  onRetry: () => void
}

/** 聊天页的会话搜索、分类和群聊选择区域。 */
export function ConversationList({
  groupChats,
  status,
  selectedId,
  errorMessage,
  onSelect,
  onRetry,
}: ConversationListProps) {
  const [searchText, setSearchText] = useState('')
  const normalizedSearchText = searchText.trim().toLocaleLowerCase()
  const filteredGroupChats = useMemo(
    () =>
      groupChats.filter((groupChat) => {
        if (!normalizedSearchText) {
          return true
        }

        return (
          groupChat.name.toLocaleLowerCase().includes(normalizedSearchText) ||
          groupChat.members.some((member) =>
            member.nickname.toLocaleLowerCase().includes(normalizedSearchText),
          )
        )
      }),
    [groupChats, normalizedSearchText],
  )

  return (
    <Panel aria-label="会话列表">
      <header className="border-b border-border px-4 pt-5">
        <div className="flex items-center gap-2">
          <label className="relative min-w-0 flex-1">
            <span className="sr-only">搜索群聊或群成员</span>
            <Search
              className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
              strokeWidth={1.8}
              aria-hidden="true"
            />
            <input
              type="search"
              name="conversation-search"
              autoComplete="off"
              value={searchText}
              onChange={(event) => setSearchText(event.target.value)}
              placeholder="搜索私信或群聊…"
              className="h-10 w-full rounded-lg border border-transparent bg-surface-muted pr-3 pl-9 text-sm text-foreground outline-none transition-colors placeholder:text-muted-foreground focus:border-primary/35 focus:bg-surface focus:ring-2 focus:ring-primary/12"
            />
          </label>
          <Button
            variant="ghost"
            size="icon"
            disabled
            aria-label="新建会话，后续阶段开放"
            title="新建会话将在后续阶段开放"
            className="bg-surface-muted"
          >
            <Plus className="size-5" strokeWidth={1.8} aria-hidden="true" />
          </Button>
        </div>

        <div className="mt-4 grid grid-cols-3" role="tablist" aria-label="会话分类">
          <button
            type="button"
            role="tab"
            disabled
            aria-selected="false"
            className="h-11 text-sm font-medium text-muted-foreground opacity-60"
            title="私聊将在后续阶段开放"
          >
            我的私信
          </button>
          <button
            type="button"
            role="tab"
            aria-selected="true"
            className="relative h-11 text-sm font-semibold text-primary outline-none after:absolute after:right-3 after:bottom-0 after:left-3 after:h-0.5 after:rounded-full after:bg-primary focus-visible:ring-2 focus-visible:ring-ring"
          >
            群聊
          </button>
          <button
            type="button"
            role="tab"
            disabled
            aria-selected="false"
            className="h-11 text-sm font-medium text-muted-foreground opacity-60"
            title="好友私信将在后续阶段开放"
          >
            好友私信
          </button>
        </div>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto px-3 py-4">
        {(status === 'idle' || status === 'loading') && (
          <LoadingState variant="list" />
        )}
        {status === 'error' && (
          <ErrorState message={errorMessage} onRetry={onRetry} />
        )}
        {status === 'success' && groupChats.length === 0 && (
          <EmptyState
            icon={MessageCircleMore}
            title="还没有群聊"
            description="创建群聊并加入已有 AI 账号后，会话会显示在这里。"
            compact
          />
        )}
        {status === 'success' &&
          groupChats.length > 0 &&
          filteredGroupChats.length === 0 && (
            <EmptyState
              icon={Search}
              title="没有匹配的群聊"
              description="尝试搜索其他群聊名称或 AI 成员昵称。"
              compact
            />
          )}
        {status === 'success' && filteredGroupChats.length > 0 && (
          <section aria-labelledby="group-conversations-heading">
            <div className="mb-2 flex items-center justify-between px-2">
              <h2
                id="group-conversations-heading"
                className="text-xs font-medium text-muted-foreground"
              >
                群聊
              </h2>
              <span className="text-xs text-muted-foreground">
                {filteredGroupChats.length}
              </span>
            </div>
            <ul className="grid gap-1" aria-label="群聊会话">
              {filteredGroupChats.map((groupChat) => {
                const selected = selectedId === groupChat.id
                const memberNames = groupChat.members
                  .slice(0, 2)
                  .map((member) => member.nickname)
                  .join('、')

                return (
                  <li key={groupChat.id}>
                    <button
                      type="button"
                      className={cn(
                        'flex w-full min-w-0 items-center gap-3 rounded-lg px-3 py-3 text-left outline-none transition-colors duration-200',
                        'hover:bg-surface-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1',
                        selected && 'bg-primary-soft hover:bg-primary-soft',
                      )}
                      aria-current={selected ? 'true' : undefined}
                      onClick={() => onSelect(groupChat.id)}
                    >
                      <EntityAvatar name={groupChat.name} label="群" />
                      <span className="grid min-w-0 flex-1 gap-1">
                        <strong className="truncate text-sm font-semibold text-foreground">
                          {groupChat.name}
                        </strong>
                        <span className="truncate text-xs text-muted-foreground">
                          {memberNames || '暂无 AI 成员'}
                        </span>
                      </span>
                      <span className="flex shrink-0 items-center gap-1 text-xs text-muted-foreground">
                        <UsersRound className="size-3.5" aria-hidden="true" />
                        {groupChat.members.length}
                      </span>
                    </button>
                  </li>
                )
              })}
            </ul>
          </section>
        )}
      </div>
    </Panel>
  )
}
