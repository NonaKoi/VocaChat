import { MessageCircleMore, Plus, Search, UsersRound } from 'lucide-react'
import { useMemo, useState } from 'react'
import type { ConversationSummaryResponse } from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { EmptyState } from '@/components/feedback/EmptyState'
import { ErrorState } from '@/components/feedback/ErrorState'
import { LoadingState } from '@/components/feedback/LoadingState'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import type { RemoteStatus } from '@/types/remoteStatus'
import { formatMessageTime } from '@/utils/dateTime'

interface Props { conversations: ConversationSummaryResponse[]; status: RemoteStatus; selectedKey?: string; errorMessage?: string; onSelect: (item: ConversationSummaryResponse) => void; onRetry: () => void }

export function ConversationList(props: Props) {
  const [query, setQuery] = useState('')
  const [kind, setKind] = useState<'all' | 'PrivateChat' | 'GroupChat'>('all')
  const filtered = useMemo(() => props.conversations.filter((item) => (kind === 'all' || item.kind === kind) && (!query.trim() || item.displayName.toLocaleLowerCase().includes(query.trim().toLocaleLowerCase()))), [kind, props.conversations, query])
  return <section className="flex h-full min-h-0 flex-col" aria-label="会话列表">
    <header className="border-b border-border px-4 pt-5"><div className="flex gap-2"><label className="relative min-w-0 flex-1"><span className="sr-only">搜索会话</span><Search className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" /><input type="search" name="conversation-search" autoComplete="off" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="搜索聊天…" className="h-10 w-full rounded-lg bg-surface-muted pr-3 pl-9 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring" /></label><Button size="icon" variant="ghost" disabled aria-label="新建会话"><Plus className="size-5" aria-hidden="true" /></Button></div>
      <div className="mt-4 grid grid-cols-3" role="tablist" aria-label="会话分类">{([['all','全部'],['PrivateChat','私聊'],['GroupChat','群聊']] as const).map(([value,label]) => <button key={value} type="button" role="tab" aria-selected={kind === value} onClick={() => setKind(value)} className={cn('relative h-11 text-sm text-muted-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring', kind === value && 'font-semibold text-primary after:absolute after:right-3 after:bottom-0 after:left-3 after:h-0.5 after:bg-primary')}>{label}</button>)}</div></header>
    <div className="min-h-0 flex-1 overflow-y-auto px-3 py-4">{(props.status === 'idle' || props.status === 'loading') && <LoadingState variant="list" />}{props.status === 'error' && <ErrorState message={props.errorMessage} onRetry={props.onRetry} />}{props.status === 'success' && filtered.length === 0 && <EmptyState icon={MessageCircleMore} title="还没有会话" description="从好友资料发送消息，或进入一个群聊。" compact />}
      <ul className="grid gap-1">{filtered.map((item) => { const key = `${item.kind}:${item.id}`; return <li key={key}><button type="button" onClick={() => props.onSelect(item)} aria-current={props.selectedKey === key ? 'true' : undefined} className={cn('flex w-full min-w-0 items-center gap-3 rounded-lg px-3 py-3 text-left outline-none hover:bg-surface-muted focus-visible:ring-2 focus-visible:ring-ring', props.selectedKey === key && 'bg-primary-soft')}><EntityAvatar name={item.displayName} src={item.avatarUrl} label={item.kind === 'GroupChat' ? '群' : undefined} /><span className="grid min-w-0 flex-1 gap-1"><span className="flex items-center justify-between gap-2"><strong className="truncate text-sm font-semibold">{item.displayName}</strong>{item.latestMessageAt && <time className="shrink-0 text-[11px] text-muted-foreground">{formatMessageTime(item.latestMessageAt)}</time>}</span><span className="truncate text-xs text-muted-foreground">{item.latestMessageContent || (item.kind === 'PrivateChat' ? '开始一段私聊' : '群聊已创建')}</span></span>{item.kind === 'GroupChat' && <span className="flex shrink-0 items-center gap-1 text-xs text-muted-foreground"><UsersRound className="size-3.5" />{item.memberCount}</span>}</button></li> })}</ul></div>
  </section>
}
