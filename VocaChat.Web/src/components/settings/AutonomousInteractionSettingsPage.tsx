import { useEffect, useState } from 'react'
import type { ContactResponse } from '@/api/types'
import { FriendAutonomySettingsPanel } from '@/components/settings/FriendAutonomySettingsPanel'
import { GlobalAutonomySettingsPanel } from '@/components/settings/GlobalAutonomySettingsPanel'
import { AutonomousGroupChatPanel } from '@/components/settings/AutonomousGroupChatPanel'
import { RelationshipSettingsPanel } from '@/components/settings/RelationshipSettingsPanel'
import { InteractionLogsPanel } from '@/components/settings/InteractionLogsPanel'
import { cn } from '@/lib/utils'
import type { RemoteStatus } from '@/types/remoteStatus'

type SettingsTab = 'general' | 'friends' | 'relationships' | 'groupChats' | 'logs'

interface AutonomousInteractionSettingsPageProps {
  contacts?: ContactResponse[]
  contactStatus?: RemoteStatus
  contactErrorMessage?: string
  onReloadContacts?: () => void | Promise<void>
  onDirtyChange?: (hasChanges: boolean) => void
  onOpenPrivateChat?: (privateChatId: string) => void | Promise<void>
  onOpenGroupChat?: (groupChatId: string) => void | Promise<void>
}

export function AutonomousInteractionSettingsPage({
  contacts = [],
  contactStatus = 'success',
  contactErrorMessage,
  onReloadContacts = () => undefined,
  onDirtyChange,
  onOpenPrivateChat,
  onOpenGroupChat,
}: AutonomousInteractionSettingsPageProps) {
  const [activeTab, setActiveTab] = useState<SettingsTab>(getInitialTab)
  const [hasPanelChanges, setHasPanelChanges] = useState(false)

  useEffect(() => {
    onDirtyChange?.(hasPanelChanges)
  }, [hasPanelChanges, onDirtyChange])

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
              tab="general"
              selected={activeTab === 'general'}
              onClick={() => changeTab('general')}
            >
              通用设置
            </SettingsTabButton>
            <SettingsTabButton
              tab="friends"
              selected={activeTab === 'friends'}
              onClick={() => changeTab('friends')}
            >
              好友设置
            </SettingsTabButton>
            <SettingsTabButton
              tab="relationships"
              selected={activeTab === 'relationships'}
              onClick={() => changeTab('relationships')}
            >
              关系设置
            </SettingsTabButton>
            <SettingsTabButton
              tab="groupChats"
              selected={activeTab === 'groupChats'}
              onClick={() => changeTab('groupChats')}
            >
              好友群聊
            </SettingsTabButton>
            <SettingsTabButton
              tab="logs"
              selected={activeTab === 'logs'}
              onClick={() => changeTab('logs')}
            >
              互动日志
            </SettingsTabButton>
          </div>

          <div
            id="autonomy-settings-tabpanel"
            className="mt-5"
            role="tabpanel"
            aria-labelledby={`autonomy-settings-tab-${activeTab}`}
          >
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
            ) : activeTab === 'relationships' ? (
              <RelationshipSettingsPanel
                contacts={contacts}
                contactStatus={contactStatus}
                contactErrorMessage={contactErrorMessage}
                onReloadContacts={onReloadContacts}
                onDirtyChange={setHasPanelChanges}
                onOpenPrivateChat={onOpenPrivateChat}
              />
            ) : activeTab === 'groupChats' ? (
              <AutonomousGroupChatPanel
                contacts={contacts}
                contactStatus={contactStatus}
                contactErrorMessage={contactErrorMessage}
                onReloadContacts={onReloadContacts}
                onOpenGroupChat={onOpenGroupChat}
              />
            ) : (
              <InteractionLogsPanel />
            )}
          </div>
    </div>
  )
}

function SettingsTabButton({
  tab,
  selected,
  onClick,
  children,
}: {
  tab: SettingsTab
  selected: boolean
  onClick: () => void
  children: string
}) {
  return (
    <button
      type="button"
      id={`autonomy-settings-tab-${tab}`}
      role="tab"
      aria-selected={selected}
      aria-controls="autonomy-settings-tabpanel"
      tabIndex={selected ? 0 : -1}
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
  if (
    tab === 'friends'
    || tab === 'relationships'
    || tab === 'groupChats'
    || tab === 'logs'
  ) return tab
  return 'general'
}
