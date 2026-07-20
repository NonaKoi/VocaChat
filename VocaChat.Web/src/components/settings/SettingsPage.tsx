import { useEffect, useMemo, useState } from 'react'
import { Bot, Search, UserRoundCog } from 'lucide-react'
import type {
  AiAccountResponse,
  ContactResponse,
  UpdateAiAccountRequest,
} from '@/api/types'
import { AccountProfileSettingsPage } from '@/components/settings/AccountProfileSettingsPage'
import { AutonomousInteractionSettingsPage } from '@/components/settings/AutonomousInteractionSettingsPage'
import { cn } from '@/lib/utils'
import type { RemoteStatus } from '@/types/remoteStatus'

type SettingsPageId = 'accountProfiles' | 'autonomy'

interface SettingsPageProps {
  accounts: AiAccountResponse[]
  accountStatus: RemoteStatus
  accountErrorMessage?: string
  updatingAccountId?: string
  updateErrorMessage?: string
  uploadingMedia?: { accountId: string; kind: 'avatar' | 'cover' }
  mediaErrorMessage?: string
  contacts: ContactResponse[]
  contactStatus: RemoteStatus
  contactErrorMessage?: string
  onReloadAccounts: () => void
  onReloadContacts: () => void | Promise<void>
  onUpdateAccount: (
    accountId: string,
    request: UpdateAiAccountRequest,
  ) => Promise<AiAccountResponse | undefined>
  onUploadAvatar: (accountId: string, file: File) => Promise<boolean>
  onUploadCover: (accountId: string, file: File) => Promise<boolean>
  onClearAccountErrors: () => void
  onAccountChanged: () => void | Promise<void>
  onDirtyChange?: (hasChanges: boolean) => void
  onOpenPrivateChat?: (privateChatId: string) => void | Promise<void>
  onOpenGroupChat?: (groupChatId: string) => void | Promise<void>
}

const settingsItems: Array<{
  id: SettingsPageId
  label: string
  group: string
  icon: typeof Bot
}> = [
  { id: 'accountProfiles', label: '账号资料编辑', group: '账号管理', icon: UserRoundCog },
  { id: 'autonomy', label: '好友自主互动', group: '互动设置', icon: Bot },
]

/** 提供设置总导航，并让各设置页面独立管理自己的业务状态。 */
export function SettingsPage({
  accounts,
  accountStatus,
  accountErrorMessage,
  updatingAccountId,
  updateErrorMessage,
  uploadingMedia,
  mediaErrorMessage,
  contacts,
  contactStatus,
  contactErrorMessage,
  onReloadAccounts,
  onReloadContacts,
  onUpdateAccount,
  onUploadAvatar,
  onUploadCover,
  onClearAccountErrors,
  onAccountChanged,
  onDirtyChange,
  onOpenPrivateChat,
  onOpenGroupChat,
}: SettingsPageProps) {
  const [activePage, setActivePage] = useState<SettingsPageId>(getInitialSettingsPage)
  const [searchTerm, setSearchTerm] = useState('')
  const [hasPageChanges, setHasPageChanges] = useState(false)
  const normalizedSearch = searchTerm.trim().toLocaleLowerCase()
  const visibleGroups = useMemo(() => {
    const visibleItems = settingsItems.filter((item) => !normalizedSearch || item.label.toLocaleLowerCase().includes(normalizedSearch))
    return Array.from(new Set(visibleItems.map((item) => item.group))).map((group) => ({
      group,
      items: visibleItems.filter((item) => item.group === group),
    }))
  }, [normalizedSearch])

  useEffect(() => {
    onDirtyChange?.(hasPageChanges)
  }, [hasPageChanges, onDirtyChange])

  useEffect(() => {
    function warnBeforeLeaving(event: BeforeUnloadEvent) {
      if (!hasPageChanges) return
      event.preventDefault()
    }

    window.addEventListener('beforeunload', warnBeforeLeaving)
    return () => window.removeEventListener('beforeunload', warnBeforeLeaving)
  }, [hasPageChanges])

  function changePage(page: SettingsPageId) {
    if (page === activePage) return
    if (hasPageChanges && !window.confirm('设置尚未保存，确定要切换吗？')) return

    setActivePage(page)
    setHasPageChanges(false)
    const url = new URL(window.location.href)
    url.searchParams.set('settingsPage', page)
    window.history.replaceState(null, '', url)
  }

  return (
    <div className="grid h-full min-h-0 grid-cols-[208px_minmax(0,1fr)] bg-surface xl:grid-cols-[236px_minmax(0,1fr)]">
      <aside className="flex min-h-0 flex-col border-r border-border bg-surface px-4 py-7" aria-label="设置分类">
        <h1 className="px-2 font-display text-xl font-semibold tracking-[-0.02em] text-foreground">设置</h1>
        <label className="mt-6 flex h-10 items-center gap-2 rounded-lg border border-border bg-surface-muted px-3 text-muted-foreground focus-within:border-primary/40 focus-within:ring-2 focus-within:ring-primary/10">
          <Search className="size-4 shrink-0" strokeWidth={1.8} aria-hidden="true" />
          <span className="sr-only">搜索设置项</span>
          <input
            type="search"
            name="settingsSearch"
            autoComplete="off"
            value={searchTerm}
            onChange={(event) => setSearchTerm(event.target.value)}
            placeholder="搜索设置项…"
            className="min-w-0 flex-1 bg-transparent text-sm text-foreground outline-none placeholder:text-muted-foreground"
          />
        </label>

        <nav className="mt-6 grid gap-5" aria-label="设置页面">
          {visibleGroups.map(({ group, items }) => (
            <div key={group}>
              <p className="px-2 text-xs font-medium text-muted-foreground">{group}</p>
              <div className="mt-2 grid gap-1">
                {items.map((item) => {
                  const Icon = item.icon
                  const selected = activePage === item.id
                  return (
                    <button
                      key={item.id}
                      type="button"
                      aria-current={selected ? 'page' : undefined}
                      onClick={() => changePage(item.id)}
                      className={cn(
                        'flex w-full items-center gap-3 rounded-lg px-3 py-3 text-left text-sm font-medium text-muted-foreground outline-none transition-colors hover:bg-surface-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring',
                        selected && 'bg-primary-soft font-semibold text-primary hover:bg-primary-soft hover:text-primary',
                      )}
                    >
                      <Icon className="size-[18px]" strokeWidth={1.8} aria-hidden="true" />
                      {item.label}
                    </button>
                  )
                })}
              </div>
            </div>
          ))}
          {visibleGroups.length === 0 && <p className="px-3 py-4 text-xs leading-5 text-muted-foreground">没有匹配的设置项</p>}
        </nav>

        <p className="mt-auto px-2 pt-8 text-xs leading-5 text-muted-foreground">设置数据保存在此设备。</p>
      </aside>

      <main className="min-h-0 min-w-0 overflow-hidden bg-surface-muted">
        {activePage === 'accountProfiles' ? (
          <AccountProfileSettingsPage
            accounts={accounts}
            accountStatus={accountStatus}
            accountErrorMessage={accountErrorMessage}
            updatingAccountId={updatingAccountId}
            updateErrorMessage={updateErrorMessage}
            uploadingMedia={uploadingMedia}
            mediaErrorMessage={mediaErrorMessage}
            onReloadAccounts={onReloadAccounts}
            onUpdateAccount={onUpdateAccount}
            onUploadAvatar={onUploadAvatar}
            onUploadCover={onUploadCover}
            onClearAccountErrors={onClearAccountErrors}
            onAccountChanged={onAccountChanged}
            onDirtyChange={setHasPageChanges}
          />
        ) : (
          <div className="h-full overflow-y-auto px-6 py-8 xl:px-8">
            <AutonomousInteractionSettingsPage
              contacts={contacts}
              contactStatus={contactStatus}
              contactErrorMessage={contactErrorMessage}
              onReloadContacts={onReloadContacts}
              onDirtyChange={setHasPageChanges}
              onOpenPrivateChat={onOpenPrivateChat}
              onOpenGroupChat={onOpenGroupChat}
            />
          </div>
        )}
      </main>
    </div>
  )
}

function getInitialSettingsPage(): SettingsPageId {
  return new URLSearchParams(window.location.search).get('settingsPage') === 'accountProfiles'
    ? 'accountProfiles'
    : 'autonomy'
}
