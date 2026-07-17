import { useEffect, useState } from 'react'
import { Bot, Search } from 'lucide-react'
import type { ContactResponse } from '@/api/types'
import { FriendAutonomySettingsPanel } from '@/components/settings/FriendAutonomySettingsPanel'
import { GlobalAutonomySettingsPanel } from '@/components/settings/GlobalAutonomySettingsPanel'
import { RelationshipSettingsPanel } from '@/components/settings/RelationshipSettingsPanel'
import { cn } from '@/lib/utils'
import type { RemoteStatus } from '@/types/remoteStatus'

type SettingsTab = 'general' | 'friends' | 'relationships'

interface AutonomousInteractionSettingsPageProps {
  contacts?: ContactResponse[]
  contactStatus?: RemoteStatus
  contactErrorMessage?: string
  onReloadContacts?: () => void | Promise<void>
  onDirtyChange?: (hasChanges: boolean) => void
}

export function AutonomousInteractionSettingsPage({
  contacts = [],
  contactStatus = 'success',
  contactErrorMessage,
  onReloadContacts = () => undefined,
  onDirtyChange,
}: AutonomousInteractionSettingsPageProps) {
  const [searchTerm, setSearchTerm] = useState('')
  const [activeTab, setActiveTab] = useState<SettingsTab>(getInitialTab)
  const [hasPanelChanges, setHasPanelChanges] = useState(false)
  const matchesSearch = '好友自主互动'.includes(searchTerm.trim())

  useEffect(() => {
    onDirtyChange?.(hasPanelChanges)
  }, [hasPanelChanges, onDirtyChange])

  useEffect(() => {
    function warnBeforeLeaving(event: BeforeUnloadEvent) {
      if (!hasPanelChanges) return
      event.preventDefault()
    }

    window.addEventListener('beforeunload', warnBeforeLeaving)
    return () => window.removeEventListener('beforeunload', warnBeforeLeaving)
  }, [hasPanelChanges])

  function changeTab(tab: SettingsTab) {
    if (tab === activeTab) return
    if (
      hasPanelChanges
      && !window.confirm('设置尚未保存，确定要切换吗？')
    ) {
      return
    }

    setHasPanelChanges(false)
    setActiveTab(tab)
    const url = new URL(window.location.href)
    url.searchParams.set('settingsTab', tab)
    window.history.replaceState(null, '', url)
  }

  return (
    <div className="grid h-full min-h-0 grid-cols-[236px_minmax(0,1fr)] bg-surface">
      <aside className="flex min-h-0 flex-col border-r border-border bg-surface px-4 py-7" aria-label="设置分类">
        <h1 className="px-2 font-display text-xl font-semibold tracking-[-0.02em] text-foreground">
          设置
        </h1>
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

        <div className="mt-6">
          <p className="px-2 text-xs font-medium text-muted-foreground">互动设置</p>
          {matchesSearch ? (
            <div
              aria-current="page"
              className="mt-2 flex w-full items-center gap-3 rounded-lg bg-primary-soft px-3 py-3 text-left text-sm font-semibold text-primary"
            >
              <Bot className="size-[18px]" strokeWidth={1.8} aria-hidden="true" />
              好友自主互动
            </div>
          ) : (
            <p className="px-3 py-4 text-xs leading-5 text-muted-foreground">
              没有匹配的设置项
            </p>
          )}
        </div>

        <p className="mt-auto px-2 pt-8 text-xs leading-5 text-muted-foreground">
          设置数据保存在此设备。
        </p>
      </aside>

      <main className="min-h-0 min-w-0 overflow-y-auto bg-surface-muted px-6 py-8 xl:px-8">
        <div className="mx-auto w-full max-w-[1180px]">
          <header>
            <h2 className="font-display text-2xl font-semibold tracking-[-0.025em] text-foreground">
              好友自主互动
            </h2>
            <p className="mt-1 text-sm leading-6 text-muted-foreground">
              管理好友之间自主私信和好友群聊的整体范围与个人权限。
            </p>
          </header>

          <div className="mt-6 flex gap-1 border-b border-border" role="tablist" aria-label="好友自主互动设置范围">
            <SettingsTabButton
              selected={activeTab === 'general'}
              onClick={() => changeTab('general')}
            >
              通用设置
            </SettingsTabButton>
            <SettingsTabButton
              selected={activeTab === 'friends'}
              onClick={() => changeTab('friends')}
            >
              好友设置
            </SettingsTabButton>
            <SettingsTabButton
              selected={activeTab === 'relationships'}
              onClick={() => changeTab('relationships')}
            >
              关系设置
            </SettingsTabButton>
          </div>

          <div className="mt-5" role="tabpanel">
            {activeTab === 'general' ? (
              <GlobalAutonomySettingsPanel onDirtyChange={setHasPanelChanges} />
            ) : activeTab === 'friends' ? (
              <FriendAutonomySettingsPanel
                contacts={contacts}
                contactStatus={contactStatus}
                contactErrorMessage={contactErrorMessage}
                onReloadContacts={onReloadContacts}
                onDirtyChange={setHasPanelChanges}
              />
            ) : (
              <RelationshipSettingsPanel
                contacts={contacts}
                contactStatus={contactStatus}
                contactErrorMessage={contactErrorMessage}
                onReloadContacts={onReloadContacts}
                onDirtyChange={setHasPanelChanges}
              />
            )}
          </div>
        </div>
      </main>
    </div>
  )
}

function SettingsTabButton({
  selected,
  onClick,
  children,
}: {
  selected: boolean
  onClick: () => void
  children: string
}) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={selected}
      onClick={onClick}
      className={cn(
        'relative px-4 py-2.5 text-sm font-medium text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset',
        selected && 'text-primary after:absolute after:inset-x-3 after:bottom-[-1px] after:h-0.5 after:bg-primary',
      )}
    >
      {children}
    </button>
  )
}

function getInitialTab(): SettingsTab {
  const tab = new URLSearchParams(window.location.search).get('settingsTab')
  if (tab === 'friends' || tab === 'relationships') return tab
  return 'general'
}
