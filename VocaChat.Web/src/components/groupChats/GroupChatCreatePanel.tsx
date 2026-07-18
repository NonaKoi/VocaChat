import { ArrowLeft, Check, Search, UserRound, UsersRound } from 'lucide-react'
import { useMemo, useState } from 'react'
import type { ContactResponse, CreateGroupChatRequest } from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { ErrorState } from '@/components/feedback/ErrorState'
import { LoadingState } from '@/components/feedback/LoadingState'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import type { RemoteStatus } from '@/types/remoteStatus'

interface GroupChatCreatePanelProps {
  contacts: ContactResponse[]
  contactStatus: RemoteStatus
  contactErrorMessage?: string
  isSubmitting: boolean
  errorMessage?: string
  onRetryContacts: () => void
  onCancel: () => void
  onCreate: (request: CreateGroupChatRequest) => Promise<unknown>
}

/** 在聊天工作区内完成群名、参与关系和好友成员选择。 */
export function GroupChatCreatePanel({
  contacts,
  contactStatus,
  contactErrorMessage,
  isSubmitting,
  errorMessage,
  onRetryContacts,
  onCancel,
  onCreate,
}: GroupChatCreatePanelProps) {
  const [name, setName] = useState('')
  const [includesLocalUser, setIncludesLocalUser] = useState(true)
  const [query, setQuery] = useState('')
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [validationMessage, setValidationMessage] = useState<string>()

  const filteredContacts = useMemo(() => {
    const normalizedQuery = query.trim().toLocaleLowerCase()
    if (!normalizedQuery) return contacts

    return contacts.filter(({ friend }) => (
      friend.nickname.toLocaleLowerCase().includes(normalizedQuery)
      || friend.vcNumber.toLocaleLowerCase().includes(normalizedQuery)
    ))
  }, [contacts, query])

  const selectedContacts = useMemo(
    () => contacts.filter((contact) => selectedIds.includes(contact.friend.id)),
    [contacts, selectedIds],
  )

  function toggleMember(aiAccountId: string) {
    setValidationMessage(undefined)
    setSelectedIds((current) => current.includes(aiAccountId)
      ? current.filter((id) => id !== aiAccountId)
      : [...current, aiAccountId])
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const trimmedName = name.trim()

    if (!trimmedName) {
      setValidationMessage('请输入群聊名称。')
      return
    }

    if (selectedIds.length === 0) {
      setValidationMessage('请至少选择一位好友。')
      return
    }

    setValidationMessage(undefined)
    await onCreate({
      name: trimmedName,
      memberAiAccountIds: selectedIds,
      includesLocalUser,
    })
  }

  const totalMemberCount = selectedIds.length + (includesLocalUser ? 1 : 0)

  return (
    <section className="flex h-full min-h-0 flex-col bg-surface" aria-labelledby="create-group-title">
      <header className="flex h-[72px] shrink-0 items-center gap-3 border-b border-border px-6">
        <Button
          variant="ghost"
          size="icon"
          className="size-9"
          onClick={onCancel}
          aria-label="返回聊天"
        >
          <ArrowLeft className="size-[18px]" aria-hidden="true" />
        </Button>
        <div>
          <h1 id="create-group-title" className="text-base font-semibold text-foreground">
            创建群聊
          </h1>
          <p className="mt-0.5 text-xs text-muted-foreground">
            选择参与关系和好友，创建后将直接进入群聊。
          </p>
        </div>
      </header>

      <form className="min-h-0 flex-1 overflow-y-auto" onSubmit={submit} noValidate>
        <div className="mx-auto w-full max-w-4xl px-6 py-7 lg:px-10">
          <section className="border-b border-border pb-7" aria-labelledby="group-basics-heading">
            <h2 id="group-basics-heading" className="text-sm font-semibold text-foreground">
              基本信息
            </h2>
            <label className="mt-4 block max-w-xl">
              <span className="text-sm font-medium text-foreground">群聊名称</span>
              <input
                value={name}
                onChange={(event) => {
                  setName(event.target.value)
                  setValidationMessage(undefined)
                }}
                maxLength={60}
                autoFocus
                name="group-name"
                autoComplete="off"
                placeholder="例如：周末电影会…"
                className="mt-2 h-10 w-full rounded-lg border border-border bg-surface px-3 text-sm outline-none placeholder:text-muted-foreground focus-visible:border-primary/60 focus-visible:ring-2 focus-visible:ring-ring"
              />
            </label>

            <fieldset className="mt-6">
              <legend className="text-sm font-medium text-foreground">参与关系</legend>
              <div className="mt-2 grid max-w-2xl gap-2 sm:grid-cols-2">
                <ParticipationOption
                  checked={includesLocalUser}
                  title="我的群聊"
                  description="你和所选好友共同参与"
                  icon={UserRound}
                  onChange={() => setIncludesLocalUser(true)}
                />
                <ParticipationOption
                  checked={!includesLocalUser}
                  title="好友群聊"
                  description="只有所选好友参与"
                  icon={UsersRound}
                  onChange={() => setIncludesLocalUser(false)}
                />
              </div>
            </fieldset>
          </section>

          <section className="pt-7" aria-labelledby="select-members-heading">
            <div className="flex flex-wrap items-end justify-between gap-3">
              <div>
                <h2 id="select-members-heading" className="text-sm font-semibold text-foreground">
                  选择好友
                </h2>
                <p className="mt-1 text-xs text-muted-foreground">
                  已选择 {selectedIds.length} 位好友，共 {totalMemberCount} 位成员
                </p>
              </div>
              <label className="relative w-full sm:w-64">
                <span className="sr-only">搜索好友</span>
                <Search
                  className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
                  aria-hidden="true"
                />
                <input
                  type="search"
                  name="group-member-search"
                  autoComplete="off"
                  value={query}
                  onChange={(event) => setQuery(event.target.value)}
                  placeholder="搜索昵称或 VC 号…"
                  className="h-9 w-full rounded-lg bg-surface-muted pr-3 pl-9 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
                />
              </label>
            </div>

            {(contactStatus === 'idle' || contactStatus === 'loading') && (
              <div className="mt-5"><LoadingState variant="list" /></div>
            )}
            {contactStatus === 'error' && (
              <div className="mt-5">
                <ErrorState message={contactErrorMessage} onRetry={onRetryContacts} />
              </div>
            )}
            {contactStatus === 'success' && contacts.length === 0 && (
              <div className="mt-5 border-y border-border py-10 text-center">
                <UsersRound className="mx-auto size-6 text-muted-foreground" aria-hidden="true" />
                <p className="mt-2 text-sm font-medium text-foreground">还没有可选择的好友</p>
                <p className="mt-1 text-xs text-muted-foreground">请先在好友页寻找并添加好友。</p>
              </div>
            )}
            {contactStatus === 'success' && contacts.length > 0 && filteredContacts.length === 0 && (
              <p className="mt-5 border-y border-border py-8 text-center text-sm text-muted-foreground">
                没有找到匹配的好友
              </p>
            )}
            {contactStatus === 'success' && filteredContacts.length > 0 && (
              <ul className="mt-5 grid gap-2 md:grid-cols-2" aria-label="可选择的好友">
                {filteredContacts.map((contact) => {
                  const checked = selectedIds.includes(contact.friend.id)
                  return (
                    <li key={contact.id}>
                      <label
                        className={cn(
                          'flex cursor-pointer items-center gap-3 rounded-lg border px-3 py-3 outline-none transition-colors hover:bg-surface-muted focus-within:ring-2 focus-within:ring-ring',
                          checked ? 'border-primary/45 bg-primary-soft' : 'border-border bg-surface',
                        )}
                      >
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={() => toggleMember(contact.friend.id)}
                          className="sr-only"
                        />
                        <EntityAvatar
                          name={contact.friend.nickname}
                          src={contact.friend.avatarUrl}
                          size="small"
                        />
                        <span className="min-w-0 flex-1">
                          <span className="block truncate text-sm font-medium text-foreground">
                            {contact.friend.nickname}
                          </span>
                          <span className="block truncate text-xs text-muted-foreground">
                            VC号：{contact.friend.vcNumber}
                          </span>
                        </span>
                        <span
                          className={cn(
                            'grid size-5 shrink-0 place-items-center rounded-md border',
                            checked
                              ? 'border-primary bg-primary text-primary-foreground'
                              : 'border-border bg-surface',
                          )}
                          aria-hidden="true"
                        >
                          {checked && <Check className="size-3.5" />}
                        </span>
                      </label>
                    </li>
                  )
                })}
              </ul>
            )}

            {selectedContacts.length > 0 && (
              <div className="mt-5 flex items-center gap-2" aria-label="已选择的好友">
                <div className="flex -space-x-2">
                  {selectedContacts.slice(0, 6).map((contact) => (
                    <EntityAvatar
                      key={contact.id}
                      name={contact.friend.nickname}
                      src={contact.friend.avatarUrl}
                      size="small"
                      className="border-2 border-surface"
                    />
                  ))}
                </div>
                {selectedContacts.length > 6 && (
                  <span className="text-xs text-muted-foreground">
                    另有 {selectedContacts.length - 6} 位
                  </span>
                )}
              </div>
            )}
          </section>

          {(validationMessage || errorMessage) && (
            <p
              className="mt-6 text-sm text-destructive"
              role="alert"
              aria-live="polite"
            >
              {validationMessage || errorMessage}
            </p>
          )}

          <div className="mt-7 flex justify-end gap-2 border-t border-border pt-5">
            <Button variant="outline" onClick={onCancel} disabled={isSubmitting}>
              取消
            </Button>
            <Button type="submit" disabled={isSubmitting || contactStatus !== 'success'}>
              {isSubmitting ? '正在创建…' : '创建并进入群聊'}
            </Button>
          </div>
        </div>
      </form>
    </section>
  )
}

interface ParticipationOptionProps {
  checked: boolean
  title: string
  description: string
  icon: typeof UserRound
  onChange: () => void
}

function ParticipationOption({
  checked,
  title,
  description,
  icon: Icon,
  onChange,
}: ParticipationOptionProps) {
  return (
    <label
      className={cn(
        'flex cursor-pointer items-center gap-3 rounded-lg border px-4 py-3 transition-colors hover:bg-surface-muted focus-within:ring-2 focus-within:ring-ring',
        checked ? 'border-primary/45 bg-primary-soft' : 'border-border',
      )}
    >
      <input
        type="radio"
        name="group-participation"
        checked={checked}
        onChange={onChange}
        className="sr-only"
      />
      <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-surface-muted text-primary">
        <Icon className="size-[18px]" aria-hidden="true" />
      </span>
      <span className="min-w-0">
        <span className="block text-sm font-semibold text-foreground">{title}</span>
        <span className="mt-0.5 block text-xs text-muted-foreground">{description}</span>
      </span>
      <span
        className={cn(
          'ml-auto size-4 shrink-0 rounded-full border-[5px]',
          checked ? 'border-primary' : 'border-border',
        )}
        aria-hidden="true"
      />
    </label>
  )
}
