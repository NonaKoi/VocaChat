import { Plus, Search, UserRound } from 'lucide-react'
import { useMemo, useState } from 'react'
import type { ContactGroupResponse, ContactResponse } from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { EmptyState } from '@/components/feedback/EmptyState'
import { ErrorState } from '@/components/feedback/ErrorState'
import { LoadingState } from '@/components/feedback/LoadingState'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import type { RemoteStatus } from '@/types/remoteStatus'

interface ContactListProps {
  contacts: ContactResponse[]
  groups: ContactGroupResponse[]
  status: RemoteStatus
  selectedAccountId?: string
  isCreating: boolean
  errorMessage?: string
  onSelect: (accountId: string) => void
  onCreate: () => void
  onRetry: () => void
}

export function ContactList(props: ContactListProps) {
  const [query, setQuery] = useState('')
  const normalized = query.trim().toLocaleLowerCase()
  const grouped = useMemo(() => props.groups.map((group) => ({
    group,
    contacts: props.contacts.filter((contact) => contact.contactGroupId === group.id && (
      !normalized || contact.friend.nickname.toLocaleLowerCase().includes(normalized) || contact.friend.vcNumber.toLocaleLowerCase().includes(normalized)
    )),
  })).filter((item) => item.contacts.length > 0), [normalized, props.contacts, props.groups])

  return (
    <section className="flex h-full min-h-0 flex-col" aria-label="好友列表">
      <header className="border-b border-border px-4 py-5">
        <div className="flex items-center gap-2">
          <label className="relative min-w-0 flex-1">
            <span className="sr-only">搜索好友</span>
            <Search className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
            <input type="search" name="contact-search" autoComplete="off" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="搜索好友或 VC 号…" className="h-10 w-full rounded-lg bg-surface-muted pr-3 pl-9 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring" />
          </label>
          <Button size="icon" variant="ghost" className="bg-surface-muted" onClick={props.onCreate} aria-label="寻找新朋友"><Plus className="size-5" aria-hidden="true" /></Button>
        </div>
        <h1 className="mt-5 text-base font-semibold">好友</h1>
        <p className="mt-1 text-xs text-muted-foreground">{props.contacts.length} 位朋友</p>
      </header>
      <div className="min-h-0 flex-1 overflow-y-auto px-3 py-4">
        {(props.status === 'idle' || props.status === 'loading') && <LoadingState variant="list" />}
        {props.status === 'error' && <ErrorState message={props.errorMessage} onRetry={props.onRetry} />}
        {props.status === 'success' && props.contacts.length === 0 && <EmptyState icon={UserRound} title="还没有好友" description="从寻找新朋友开始，认识第一位长期陪伴的伙伴。" compact />}
        {props.status === 'success' && props.contacts.length > 0 && grouped.length === 0 && <EmptyState icon={Search} title="没有匹配的好友" description="尝试搜索其他昵称或 VC 号。" compact />}
        {grouped.map(({ group, contacts }) => (
          <section key={group.id} className="mb-5" aria-labelledby={`contact-group-${group.id}`}>
            <div className="mb-2 flex items-center justify-between px-2 text-xs text-muted-foreground"><h2 id={`contact-group-${group.id}`}>{group.name}</h2><span>{contacts.length}</span></div>
            <ul className="grid gap-1">
              {contacts.map((contact) => <li key={contact.id}><button type="button" onClick={() => props.onSelect(contact.friend.id)} aria-current={props.selectedAccountId === contact.friend.id ? 'true' : undefined} className={cn('flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left outline-none hover:bg-surface-muted focus-visible:ring-2 focus-visible:ring-ring', props.selectedAccountId === contact.friend.id && 'bg-primary-soft')}>
                <EntityAvatar name={contact.friend.nickname} src={contact.friend.avatarUrl} size="small" />
                <span className="min-w-0 flex-1"><strong className="block truncate text-sm font-semibold">{contact.friend.nickname}</strong><span className="block truncate text-xs text-muted-foreground">{contact.friend.signature || `VC号：${contact.friend.vcNumber}`}</span></span>
                <span className={cn('size-2 rounded-full', contact.friend.onlineStatus === 'Online' ? 'bg-success' : 'bg-muted-foreground/40')} aria-label={contact.friend.onlineStatus === 'Online' ? '在线' : '离线'} />
              </button></li>)}
            </ul>
          </section>
        ))}
      </div>
    </section>
  )
}
