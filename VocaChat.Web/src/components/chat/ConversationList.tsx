import { MessageCircleMore, Plus, Search, UsersRound } from 'lucide-react'
import { useMemo, useState } from 'react'
import type {
  ConversationCategory,
  ConversationSummaryResponse,
} from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { EmptyState } from '@/components/feedback/EmptyState'
import { ErrorState } from '@/components/feedback/ErrorState'
import { LoadingState } from '@/components/feedback/LoadingState'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import type { RemoteStatus } from '@/types/remoteStatus'
import { formatMessageTime } from '@/utils/dateTime'

type PrimaryFilter = 'all' | 'PrivateChat' | 'GroupChat'

interface Props {
  conversations: ConversationSummaryResponse[]
  status: RemoteStatus
  selectedKey?: string
  errorMessage?: string
  onSelect: (item: ConversationSummaryResponse) => void
  onRetry: () => void
}

const primaryFilters = [
  ['all', '全部'],
  ['PrivateChat', '私聊'],
  ['GroupChat', '群聊'],
] as const

const privateCategories = [
  ['MyPrivateChat', '我的私信'],
  ['FriendPrivateChat', '好友私信'],
] as const

const groupCategories = [
  ['MyGroupChat', '我的群聊'],
  ['FriendGroupChat', '好友群聊'],
] as const

export function ConversationList(props: Props) {
  const [query, setQuery] = useState('')
  const [primaryFilter, setPrimaryFilter] =
    useState<PrimaryFilter>('all')
  const [privateCategory, setPrivateCategory] =
    useState<ConversationCategory>('MyPrivateChat')
  const [groupCategory, setGroupCategory] =
    useState<ConversationCategory>('MyGroupChat')

  const activeCategory = primaryFilter === 'PrivateChat'
    ? privateCategory
    : primaryFilter === 'GroupChat'
      ? groupCategory
      : undefined

  const filtered = useMemo(() => {
    const normalizedQuery = query.trim().toLocaleLowerCase()

    return props.conversations.filter((item) => {
      const matchesPrimary = primaryFilter === 'all'
        || item.kind === primaryFilter
      const matchesCategory = activeCategory === undefined
        || item.category === activeCategory
      const matchesQuery = normalizedQuery.length === 0
        || item.displayName.toLocaleLowerCase().includes(normalizedQuery)

      return matchesPrimary && matchesCategory && matchesQuery
    })
  }, [activeCategory, primaryFilter, props.conversations, query])

  const emptyCopy = getEmptyCopy(activeCategory, query)

  return (
    <section className="flex h-full min-h-0 flex-col" aria-label="会话列表">
      <header className="border-b border-border px-4 pt-5">
        <div className="flex gap-2">
          <label className="relative min-w-0 flex-1">
            <span className="sr-only">搜索会话</span>
            <Search
              className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
              aria-hidden="true"
            />
            <input
              type="search"
              name="conversation-search"
              autoComplete="off"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="搜索聊天…"
              className="h-10 w-full rounded-lg bg-surface-muted pr-3 pl-9 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
            />
          </label>
          <Button size="icon" variant="ghost" disabled aria-label="新建会话">
            <Plus className="size-5" aria-hidden="true" />
          </Button>
        </div>

        <div
          className="mt-4 grid grid-cols-3"
          role="tablist"
          aria-label="会话类型"
        >
          {primaryFilters.map(([value, label]) => (
            <button
              key={value}
              type="button"
              role="tab"
              aria-selected={primaryFilter === value}
              onClick={() => setPrimaryFilter(value)}
              className={cn(
                'relative h-11 text-sm text-muted-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring',
                primaryFilter === value
                  && 'font-semibold text-primary after:absolute after:right-3 after:bottom-0 after:left-3 after:h-0.5 after:bg-primary',
              )}
            >
              {label}
            </button>
          ))}
        </div>

        {primaryFilter !== 'all' && (
          <CategoryTabs
            categories={primaryFilter === 'PrivateChat'
              ? privateCategories
              : groupCategories}
            value={activeCategory!}
            onChange={primaryFilter === 'PrivateChat'
              ? setPrivateCategory
              : setGroupCategory}
          />
        )}
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto px-3 py-4">
        {(props.status === 'idle' || props.status === 'loading') && (
          <LoadingState variant="list" />
        )}
        {props.status === 'error' && (
          <ErrorState message={props.errorMessage} onRetry={props.onRetry} />
        )}
        {props.status === 'success' && filtered.length === 0 && (
          <EmptyState
            icon={MessageCircleMore}
            title={emptyCopy.title}
            description={emptyCopy.description}
            compact
          />
        )}

        <ul className="grid gap-1">
          {filtered.map((item) => {
            const key = `${item.kind}:${item.id}`

            return (
              <li key={key}>
                <button
                  type="button"
                  onClick={() => props.onSelect(item)}
                  aria-current={props.selectedKey === key ? 'true' : undefined}
                  className={cn(
                    'flex w-full min-w-0 items-center gap-3 rounded-lg px-3 py-3 text-left outline-none hover:bg-surface-muted focus-visible:ring-2 focus-visible:ring-ring',
                    props.selectedKey === key && 'bg-primary-soft',
                  )}
                >
                  <EntityAvatar
                    name={item.displayName}
                    src={item.avatarUrl}
                    label={item.kind === 'GroupChat' ? '群' : undefined}
                  />
                  <span className="grid min-w-0 flex-1 gap-1">
                    <span className="flex items-center justify-between gap-2">
                      <strong className="truncate text-sm font-semibold">
                        {item.displayName}
                      </strong>
                      {item.latestMessageAt && (
                        <time className="shrink-0 text-[11px] text-muted-foreground">
                          {formatMessageTime(item.latestMessageAt)}
                        </time>
                      )}
                    </span>
                    <span className="truncate text-xs text-muted-foreground">
                      {item.latestMessageContent || getConversationPlaceholder(item)}
                    </span>
                  </span>
                  {item.kind === 'GroupChat' && (
                    <span className="flex shrink-0 items-center gap-1 text-xs text-muted-foreground">
                      <UsersRound className="size-3.5" aria-hidden="true" />
                      {item.memberCount}
                    </span>
                  )}
                </button>
              </li>
            )
          })}
        </ul>
      </div>
    </section>
  )
}

interface CategoryTabsProps {
  categories: ReadonlyArray<readonly [ConversationCategory, string]>
  value: ConversationCategory
  onChange: (value: ConversationCategory) => void
}

function CategoryTabs({ categories, value, onChange }: CategoryTabsProps) {
  return (
    <div
      className="mb-3 grid grid-cols-2 gap-1 rounded-lg bg-surface-muted p-1"
      role="tablist"
      aria-label="会话参与关系"
    >
      {categories.map(([category, label]) => (
        <button
          key={category}
          type="button"
          role="tab"
          aria-selected={value === category}
          onClick={() => onChange(category)}
          className={cn(
            'h-8 rounded-md px-2 text-xs font-medium text-muted-foreground outline-none hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring',
            value === category && 'bg-surface text-foreground',
          )}
        >
          {label}
        </button>
      ))}
    </div>
  )
}

function getConversationPlaceholder(item: ConversationSummaryResponse) {
  if (item.category === 'FriendPrivateChat') return '好友之间的私信'
  if (item.category === 'FriendGroupChat') return '仅好友参与的群聊'
  return item.kind === 'PrivateChat' ? '开始一段私信' : '群聊已创建'
}

function getEmptyCopy(category?: ConversationCategory, query = '') {
  if (query.trim()) {
    return {
      title: '没有找到相关会话',
      description: '可以尝试其他名称或清空搜索内容。',
    }
  }

  switch (category) {
    case 'MyPrivateChat':
      return { title: '还没有我的私信', description: '从好友资料进入一段私信。' }
    case 'FriendPrivateChat':
      return { title: '还没有好友私信', description: '好友之间的会话会显示在这里。' }
    case 'MyGroupChat':
      return { title: '还没有我的群聊', description: '你参与的群聊会显示在这里。' }
    case 'FriendGroupChat':
      return { title: '还没有好友群聊', description: '只有好友参与的群聊会显示在这里。' }
    default:
      return { title: '还没有会话', description: '从好友资料发送消息，或进入一个群聊。' }
  }
}
