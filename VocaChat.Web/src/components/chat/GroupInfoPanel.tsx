import { CalendarDays, Plus, Search, UserRound, X } from 'lucide-react'
import { useMemo, useState } from 'react'
import type { ContactResponse, GroupChatResponse } from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { Button } from '@/components/ui/button'
import { formatDateTime } from '@/utils/dateTime'
import type { RemoteStatus } from '@/types/remoteStatus'

interface GroupInfoPanelProps {
  groupChat: GroupChatResponse
  contacts: ContactResponse[]
  contactStatus: RemoteStatus
  isAddingMember: boolean
  memberErrorMessage?: string
  onClose: () => void
  onRetryContacts: () => void
  onClearMemberError: () => void
  onAddMember: (aiAccountId: string) => Promise<boolean>
}

/** 显示群聊资料，并允许从已有好友中选择一个新成员。 */
export function GroupInfoPanel({
  groupChat,
  contacts,
  contactStatus,
  isAddingMember,
  memberErrorMessage,
  onClose,
  onRetryContacts,
  onClearMemberError,
  onAddMember,
}: GroupInfoPanelProps) {
  const [memberPickerOpen, setMemberPickerOpen] = useState(false)
  const [query, setQuery] = useState('')

  const availableContacts = useMemo(() => {
    const memberIds = new Set(groupChat.members.map((member) => member.id))
    const normalizedQuery = query.trim().toLocaleLowerCase()

    return contacts.filter(({ friend }) => (
      !memberIds.has(friend.id)
      && (!normalizedQuery
        || friend.nickname.toLocaleLowerCase().includes(normalizedQuery)
        || friend.vcNumber.toLocaleLowerCase().includes(normalizedQuery))
    ))
  }, [contacts, groupChat.members, query])

  const memberCount = groupChat.members.length + (groupChat.includesLocalUser ? 1 : 0)

  async function addMember(aiAccountId: string) {
    const succeeded = await onAddMember(aiAccountId)
    if (succeeded) {
      setMemberPickerOpen(false)
      setQuery('')
    }
  }

  return (
    <aside
      className="absolute inset-y-0 right-0 z-20 flex w-[292px] shrink-0 flex-col border-l border-border bg-surface shadow-shell xl:static xl:shadow-none"
      aria-label="群聊资料"
    >
      <header className="flex h-14 shrink-0 items-center justify-between border-b border-border px-4">
        <h2 className="text-sm font-semibold text-foreground">群聊资料</h2>
        <Button
          variant="ghost"
          size="icon"
          className="size-8"
          onClick={onClose}
          aria-label="关闭群聊资料"
        >
          <X className="size-4" aria-hidden="true" />
        </Button>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-4 py-5">
        <div className="flex items-center gap-3 border-b border-border pb-5">
          <EntityAvatar name={groupChat.name} size="large" />
          <div className="min-w-0">
            <p className="truncate font-semibold text-foreground">
              {groupChat.name}
            </p>
            <p className="mt-1 text-xs text-muted-foreground">
              {memberCount} 位成员
            </p>
          </div>
        </div>

        <dl className="border-b border-border py-4 text-xs">
          <div className="flex items-start gap-2 text-muted-foreground">
            <CalendarDays className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            <div>
              <dt>创建时间</dt>
              <dd className="mt-1 text-foreground">
                {formatDateTime(groupChat.createdAt)}
              </dd>
            </div>
          </div>
        </dl>

        <section className="pt-4" aria-labelledby="group-member-heading">
          <div className="flex items-center justify-between">
            <h3
              id="group-member-heading"
              className="text-xs font-semibold tracking-wide text-muted-foreground"
            >
              群成员
            </h3>
            <div className="flex items-center gap-1">
              <span className="text-xs tabular-nums text-muted-foreground">
                {memberCount}
              </span>
              <Button
                variant="ghost"
                size="icon"
                className="size-8"
                aria-label={memberPickerOpen ? '收起好友选择' : '添加群成员'}
                aria-expanded={memberPickerOpen}
                onClick={() => {
                  onClearMemberError()
                  setMemberPickerOpen((current) => !current)
                }}
              >
                {memberPickerOpen
                  ? <X className="size-4" aria-hidden="true" />
                  : <Plus className="size-4" aria-hidden="true" />}
              </Button>
            </div>
          </div>

          {memberPickerOpen && (
            <div className="mt-3 border-y border-border bg-surface-muted px-2 py-3">
              <label className="relative block">
                <span className="sr-only">搜索可添加的好友</span>
                <Search
                  className="absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground"
                  aria-hidden="true"
                />
                <input
                  type="search"
                  name="available-group-member-search"
                  autoComplete="off"
                  value={query}
                  onChange={(event) => setQuery(event.target.value)}
                  placeholder="搜索好友…"
                  className="h-8 w-full rounded-md border border-border bg-surface pr-2 pl-8 text-xs outline-none focus-visible:ring-2 focus-visible:ring-ring"
                />
              </label>

              {(contactStatus === 'idle' || contactStatus === 'loading') && (
                <p className="py-5 text-center text-xs text-muted-foreground">正在读取好友…</p>
              )}
              {contactStatus === 'error' && (
                <div className="py-4 text-center">
                  <p className="text-xs text-destructive">好友列表加载失败</p>
                  <Button variant="outline" className="mt-2 h-8 text-xs" onClick={onRetryContacts}>
                    重试
                  </Button>
                </div>
              )}
              {contactStatus === 'success' && availableContacts.length === 0 && (
                <p className="py-5 text-center text-xs text-muted-foreground">
                  {query.trim() ? '没有找到匹配的好友' : '所有好友都已在群内'}
                </p>
              )}
              {contactStatus === 'success' && availableContacts.length > 0 && (
                <ul
                  className="mt-2 grid max-h-56 gap-1 overflow-x-hidden overflow-y-auto"
                  aria-label="可添加的好友"
                >
                  {availableContacts.map((contact) => (
                    <li key={contact.id}>
                      <div className="flex items-center gap-2 rounded-md px-1.5 py-2 hover:bg-surface">
                        <EntityAvatar
                          name={contact.friend.nickname}
                          src={contact.friend.avatarUrl}
                          size="small"
                        />
                        <span className="min-w-0 flex-1 truncate text-xs font-medium text-foreground">
                          {contact.friend.nickname}
                        </span>
                        <Button
                          variant="outline"
                          className="h-7 px-2 text-xs"
                          disabled={isAddingMember}
                          onClick={() => void addMember(contact.friend.id)}
                          aria-label={`添加 ${contact.friend.nickname} 到群聊`}
                        >
                          添加
                        </Button>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
              {memberErrorMessage && (
                <p
                  className="mt-2 text-xs text-destructive"
                  role="alert"
                  aria-live="polite"
                >
                  {memberErrorMessage}
                </p>
              )}
            </div>
          )}

          {groupChat.members.length === 0 && !groupChat.includesLocalUser ? (
            <div className="mt-5 rounded-lg border border-dashed border-border px-3 py-5 text-center">
              <UserRound
                className="mx-auto size-5 text-muted-foreground"
                aria-hidden="true"
              />
              <p className="mt-2 text-xs text-muted-foreground">暂无好友</p>
            </div>
          ) : (
            <ul className="mt-3 grid gap-1">
              {groupChat.includesLocalUser && (
                <li className="flex items-center gap-3 rounded-lg px-2 py-2">
                  <EntityAvatar name="我" size="small" />
                  <span className="min-w-0 flex-1 truncate text-sm font-medium text-foreground">
                    我
                  </span>
                  <span className="text-[11px] text-primary">群成员</span>
                </li>
              )}
              {groupChat.members.map((member) => (
                <li
                  key={member.id}
                  className="flex items-center gap-3 rounded-lg px-2 py-2 hover:bg-surface-muted"
                >
                  <EntityAvatar
                    name={member.nickname}
                    src={member.avatarUrl}
                    size="small"
                  />
                  <span className="min-w-0 flex-1 truncate text-sm text-foreground">
                    {member.nickname}
                  </span>
                  <span className="text-[11px] text-muted-foreground">好友</span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </aside>
  )
}
