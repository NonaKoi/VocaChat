import { useEffect, useMemo, useState } from 'react'
import { ArrowRight, Search, UsersRound } from 'lucide-react'
import type {
  AiRelationshipResponse,
  ContactResponse,
  UpdateAiRelationshipRequest,
} from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { EmptyState } from '@/components/feedback/EmptyState'
import { ErrorState } from '@/components/feedback/ErrorState'
import { SettingsSaveBar } from '@/components/settings/GlobalAutonomySettingsPanel'
import { PrivateChatDecisionPreview } from '@/components/settings/PrivateChatDecisionPreview'
import { Skeleton } from '@/components/ui/skeleton'
import { useAiRelationship } from '@/hooks/useAiRelationship'
import { cn } from '@/lib/utils'
import type { RemoteStatus } from '@/types/remoteStatus'

interface RelationshipSettingsPanelProps {
  contacts: ContactResponse[]
  contactStatus: RemoteStatus
  contactErrorMessage?: string
  onReloadContacts: () => void | Promise<void>
  onDirtyChange: (hasChanges: boolean) => void
}

/** 编辑一个好友对另一个好友形成的有方向关系。 */
export function RelationshipSettingsPanel({
  contacts,
  contactStatus,
  contactErrorMessage,
  onReloadContacts,
  onDirtyChange,
}: RelationshipSettingsPanelProps) {
  const [searchTerm, setSearchTerm] = useState('')
  const [fromAiAccountId, setFromAiAccountId] = useState<string | undefined>(
    () => new URLSearchParams(window.location.search).get('relationshipFrom')
      ?? undefined,
  )
  const [toAiAccountId, setToAiAccountId] = useState<string | undefined>(
    () => new URLSearchParams(window.location.search).get('relationshipTo')
      ?? undefined,
  )
  const relationship = useAiRelationship(fromAiAccountId, toAiAccountId)
  const reverseRelationship = useAiRelationship(toAiAccountId, fromAiAccountId)
  const [draft, setDraft] = useState<UpdateAiRelationshipRequest>()
  const [didSave, setDidSave] = useState(false)

  const fromContact = contacts.find(
    (contact) => contact.friend.id === fromAiAccountId,
  )
  const toContact = contacts.find(
    (contact) => contact.friend.id === toAiAccountId,
  )

  useEffect(() => {
    if (contacts.length < 2) {
      setFromAiAccountId(undefined)
      setToAiAccountId(undefined)
      return
    }

    const validFrom = contacts.some(
      (contact) => contact.friend.id === fromAiAccountId,
    )
    const nextFromId = validFrom ? fromAiAccountId : contacts[0].friend.id
    const validTo = contacts.some(
      (contact) => contact.friend.id === toAiAccountId
        && contact.friend.id !== nextFromId,
    )
    const nextToId = validTo
      ? toAiAccountId
      : contacts.find((contact) => contact.friend.id !== nextFromId)!.friend.id

    if (nextFromId !== fromAiAccountId) setFromAiAccountId(nextFromId)
    if (nextToId !== toAiAccountId) setToAiAccountId(nextToId)
  }, [contacts, fromAiAccountId, toAiAccountId])

  useEffect(() => {
    setDidSave(false)
    setDraft(relationship.data ? toUpdateRequest(relationship.data) : undefined)
  }, [relationship.data, fromAiAccountId, toAiAccountId])

  useEffect(() => {
    if (!fromAiAccountId || !toAiAccountId) return

    const url = new URL(window.location.href)
    url.searchParams.set('relationshipFrom', fromAiAccountId)
    url.searchParams.set('relationshipTo', toAiAccountId)
    window.history.replaceState(null, '', url)
  }, [fromAiAccountId, toAiAccountId])

  const hasChanges = useMemo(
    () => Boolean(
      draft
      && relationship.data
      && !areRelationshipsEqual(draft, relationship.data),
    ),
    [draft, relationship.data],
  )

  useEffect(() => {
    onDirtyChange(hasChanges)
  }, [hasChanges, onDirtyChange])

  const visibleContacts = useMemo(() => {
    const keyword = searchTerm.trim().toLocaleLowerCase('zh-CN')
    if (!keyword) return contacts
    return contacts.filter((contact) =>
      contact.friend.nickname.toLocaleLowerCase('zh-CN').includes(keyword)
      || contact.friend.vcNumber.toLocaleLowerCase('zh-CN').includes(keyword))
  }, [contacts, searchTerm])

  function changeFrom(accountId: string) {
    if (accountId === fromAiAccountId) return
    if (hasChanges && !window.confirm('当前关系尚未保存，确定要切换吗？')) return

    const nextToId = accountId === toAiAccountId
      ? contacts.find((contact) => contact.friend.id !== accountId)?.friend.id
      : toAiAccountId
    setFromAiAccountId(accountId)
    setToAiAccountId(nextToId)
    setDraft(undefined)
    setDidSave(false)
  }

  function changeTo(accountId: string) {
    if (accountId === toAiAccountId) return
    if (hasChanges && !window.confirm('当前关系尚未保存，确定要切换吗？')) return

    setToAiAccountId(accountId)
    setDraft(undefined)
    setDidSave(false)
  }

  async function saveChanges() {
    if (!draft) return
    const saved = await relationship.save(draft)
    if (saved) {
      setDraft(toUpdateRequest(saved))
      setDidSave(true)
    }
  }

  if (contactStatus === 'loading' || contactStatus === 'idle') {
    return <RelationshipLoadingState />
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

  if (contacts.length < 2) {
    return (
      <div className="min-h-96 overflow-hidden rounded-xl border border-border bg-surface">
        <EmptyState
          icon={UsersRound}
          title="需要至少两位好友"
          description="好友之间的关系具有方向，需要选择两位不同的好友。"
        />
      </div>
    )
  }

  return (
    <div className="grid min-h-[540px] overflow-hidden rounded-xl border border-border bg-surface lg:grid-cols-[270px_minmax(0,1fr)]">
      <aside className="flex min-h-0 flex-col border-b border-border bg-surface lg:border-r lg:border-b-0" aria-label="关系发起方列表">
        <div className="border-b border-border px-4 py-4">
          <h3 className="text-sm font-semibold text-foreground">关系发起方</h3>
          <label className="mt-3 flex h-9 items-center gap-2 rounded-lg border border-border bg-surface-muted px-3 text-muted-foreground focus-within:border-primary/40 focus-within:ring-2 focus-within:ring-primary/10">
            <Search className="size-4 shrink-0" strokeWidth={1.8} aria-hidden="true" />
            <span className="sr-only">搜索好友</span>
            <input
              type="search"
              name="relationshipFriendSearch"
              autoComplete="off"
              value={searchTerm}
              onChange={(event) => setSearchTerm(event.target.value)}
              placeholder="搜索好友…"
              className="min-w-0 flex-1 bg-transparent text-sm text-foreground outline-none placeholder:text-muted-foreground"
            />
          </label>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto p-2">
          {visibleContacts.map((contact) => {
            const selected = contact.friend.id === fromAiAccountId
            return (
              <button
                key={contact.id}
                type="button"
                aria-current={selected ? 'true' : undefined}
                onClick={() => changeFrom(contact.friend.id)}
                className={cn(
                  'flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left outline-none transition-colors hover:bg-surface-muted focus-visible:ring-2 focus-visible:ring-ring',
                  selected && 'bg-primary-soft',
                )}
              >
                <EntityAvatar
                  name={contact.friend.nickname}
                  src={contact.friend.avatarUrl}
                  size="small"
                />
                <span className="min-w-0 flex-1">
                  <span className={cn('block truncate text-sm font-medium text-foreground', selected && 'text-primary')}>
                    {contact.friend.nickname}
                  </span>
                  <span className="mt-0.5 block truncate text-xs text-muted-foreground">
                    VC号 {contact.friend.vcNumber}
                  </span>
                </span>
              </button>
            )
          })}
        </div>
      </aside>

      <div className="min-w-0 bg-surface-muted p-4 xl:p-5">
        {(relationship.status === 'loading' || relationship.status === 'idle') && (
          <RelationshipDetailLoadingState />
        )}
        {relationship.status === 'error' && (
          <div className="max-w-xl">
            <ErrorState message={relationship.errorMessage} onRetry={relationship.reload} />
          </div>
        )}
        {relationship.status === 'success' && fromContact && toContact && draft && (
          <div className="grid items-start gap-4 xl:grid-cols-[minmax(0,1fr)_240px]">
            <section className="overflow-hidden rounded-xl border border-border bg-surface" aria-labelledby="relationship-settings-title">
              <div className="border-b border-border px-5 py-4">
                <div className="flex flex-wrap items-center justify-between gap-4">
                  <div className="flex min-w-0 items-center gap-2">
                    <EntityAvatar name={fromContact.friend.nickname} src={fromContact.friend.avatarUrl} size="small" />
                    <ArrowRight className="size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                    <EntityAvatar name={toContact.friend.nickname} src={toContact.friend.avatarUrl} size="small" />
                    <div className="min-w-0">
                      <h3 id="relationship-settings-title" className="truncate text-base font-semibold text-foreground">
                        {fromContact.friend.nickname} 对 {toContact.friend.nickname}
                      </h3>
                      <p className="mt-0.5 text-xs text-muted-foreground">当前方向的好友关系</p>
                    </div>
                  </div>

                  <label className="grid gap-1 text-xs font-medium text-muted-foreground">
                    关系对象
                    <select
                      name="relationshipTarget"
                      value={toAiAccountId}
                      onChange={(event) => changeTo(event.target.value)}
                      className="h-9 min-w-40 rounded-lg border border-border bg-surface px-3 text-sm text-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring"
                    >
                      {contacts
                        .filter((contact) => contact.friend.id !== fromAiAccountId)
                        .map((contact) => (
                          <option key={contact.id} value={contact.friend.id}>
                            {contact.friend.nickname}
                          </option>
                        ))}
                    </select>
                  </label>
                </div>
              </div>

              <div className="divide-y divide-border">
                <RelationshipRange
                  id="relationship-familiarity"
                  label="熟悉度"
                  description="表示双方积累了多少共同经历和了解。"
                  value={draft.familiarity}
                  min={0}
                  max={100}
                  onChange={(familiarity) => setDraft((current) => current ? { ...current, familiarity } : current)}
                />
                <RelationshipRange
                  id="relationship-affinity"
                  label="好感度"
                  description="表示这位好友对关系对象的正面或负面倾向。"
                  value={draft.affinity}
                  min={-100}
                  max={100}
                  onChange={(affinity) => setDraft((current) => current ? { ...current, affinity } : current)}
                />
                <RelationshipRange
                  id="relationship-trust"
                  label="信任度"
                  description="表示这位好友愿意相信和依赖关系对象的程度。"
                  value={draft.trust}
                  min={0}
                  max={100}
                  onChange={(trust) => setDraft((current) => current ? { ...current, trust } : current)}
                />
              </div>

              <SettingsSaveBar
                hasChanges={hasChanges}
                didSave={didSave}
                isSaving={relationship.isSaving}
                errorMessage={relationship.saveErrorMessage}
                onSave={() => void saveChanges()}
              />
            </section>

            <aside className="overflow-hidden rounded-xl border border-border bg-surface" aria-label="反向关系摘要">
              <div className="border-b border-border px-5 py-4">
                <h3 className="text-sm font-semibold text-foreground">反向关系</h3>
                <p className="mt-1 truncate text-xs text-muted-foreground">
                  {toContact.friend.nickname} 对 {fromContact.friend.nickname}
                </p>
              </div>
              {reverseRelationship.status === 'success' && reverseRelationship.data ? (
                <dl className="divide-y divide-border px-5">
                  <RelationshipSummaryRow label="熟悉度" value={reverseRelationship.data.familiarity} />
                  <RelationshipSummaryRow label="好感度" value={reverseRelationship.data.affinity} signed />
                  <RelationshipSummaryRow label="信任度" value={reverseRelationship.data.trust} />
                  <RelationshipSummaryRow label="互动次数" value={reverseRelationship.data.interactionCount} />
                </dl>
              ) : reverseRelationship.status === 'error' ? (
                <p className="p-5 text-xs leading-5 text-destructive">反向关系读取失败。</p>
              ) : (
                <div className="grid gap-3 p-5" role="status" aria-label="正在加载反向关系">
                  <Skeleton className="h-9 rounded-lg" />
                  <Skeleton className="h-9 rounded-lg" />
                  <Skeleton className="h-9 rounded-lg" />
                </div>
              )}
              <p className="m-4 rounded-lg bg-primary-soft p-3 text-xs leading-5 text-primary">
                两个方向分别保存，因此双方对同一段关系可以有不同感受。
              </p>
            </aside>

            <PrivateChatDecisionPreview
              firstContact={fromContact}
              secondContact={toContact}
              hasUnsavedRelationship={hasChanges}
            />
          </div>
        )}
      </div>
    </div>
  )
}

function RelationshipRange({
  id,
  label,
  description,
  value,
  min,
  max,
  onChange,
}: {
  id: string
  label: string
  description: string
  value: number
  min: number
  max: number
  onChange: (value: number) => void
}) {
  return (
    <div className="px-5 py-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <label htmlFor={id} className="text-sm font-medium text-foreground">{label}</label>
          <p className="mt-1 text-xs leading-5 text-muted-foreground">{description}</p>
        </div>
        <output htmlFor={id} className="min-w-10 rounded-md bg-primary-soft px-2 py-1 text-center text-sm font-semibold tabular-nums text-primary">
          {value > 0 && min < 0 ? '+' : ''}{value}
        </output>
      </div>
      <input
        id={id}
        type="range"
        name={id}
        min={min}
        max={max}
        step={1}
        value={value}
        onChange={(event) => onChange(Number(event.target.value))}
        className="mt-4 h-2 w-full cursor-pointer accent-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
      />
      <div className="mt-1 flex justify-between text-[11px] text-muted-foreground" aria-hidden="true">
        <span>{min}</span>
        <span>{max}</span>
      </div>
    </div>
  )
}

function RelationshipSummaryRow({ label, value, signed = false }: { label: string; value: number; signed?: boolean }) {
  return (
    <div className="flex items-center justify-between gap-4 py-3">
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="text-sm font-semibold tabular-nums text-foreground">
        {signed && value > 0 ? '+' : ''}{value}
      </dd>
    </div>
  )
}

function RelationshipLoadingState() {
  return (
    <div className="grid min-h-[540px] overflow-hidden rounded-xl border border-border bg-surface lg:grid-cols-[270px_minmax(0,1fr)]" role="status" aria-label="正在加载好友关系">
      <div className="border-r border-border p-4"><Skeleton className="h-72 rounded-xl" /></div>
      <div className="bg-surface-muted p-5"><RelationshipDetailLoadingState /></div>
    </div>
  )
}

function RelationshipDetailLoadingState() {
  return (
    <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_240px]" role="status" aria-label="正在加载关系详情">
      <Skeleton className="h-[480px] rounded-xl" />
      <Skeleton className="h-80 rounded-xl" />
    </div>
  )
}

function toUpdateRequest(relationship: AiRelationshipResponse): UpdateAiRelationshipRequest {
  return {
    familiarity: relationship.familiarity,
    affinity: relationship.affinity,
    trust: relationship.trust,
  }
}

function areRelationshipsEqual(
  first: UpdateAiRelationshipRequest,
  second: AiRelationshipResponse,
) {
  return first.familiarity === second.familiarity
    && first.affinity === second.affinity
    && first.trust === second.trust
}
