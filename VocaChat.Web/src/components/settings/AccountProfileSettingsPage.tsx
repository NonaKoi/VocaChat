import { useEffect, useMemo, useState } from 'react'
import { Brain, Globe2, Search, UserRoundCog } from 'lucide-react'
import type {
  AiAccountResponse,
  UpdateAiAccountRequest,
} from '@/api/types'
import { AccountProfileEditor } from '@/components/settings/AccountProfileEditor'
import { AiSelfMemoryPanel } from '@/components/settings/AiSelfMemoryPanel'
import { AiWorldKnowledgePanel } from '@/components/settings/AiWorldKnowledgePanel'
import { ListItem } from '@/components/common/ListItem'
import { EmptyState } from '@/components/feedback/EmptyState'
import { ErrorState } from '@/components/feedback/ErrorState'
import { LoadingState } from '@/components/feedback/LoadingState'
import { cn } from '@/lib/utils'
import type { RemoteStatus } from '@/types/remoteStatus'
import { useCharacterWorlds } from '@/hooks/useCharacterWorlds'

type ProfileTab = 'profile' | 'memories' | 'worldKnowledge'

interface AccountProfileSettingsPageProps {
  accounts: AiAccountResponse[]
  accountStatus: RemoteStatus
  accountErrorMessage?: string
  updatingAccountId?: string
  updateErrorMessage?: string
  uploadingMedia?: { accountId: string; kind: 'avatar' | 'cover' }
  mediaErrorMessage?: string
  onReloadAccounts: () => void
  onUpdateAccount: (
    accountId: string,
    request: UpdateAiAccountRequest,
  ) => Promise<AiAccountResponse | undefined>
  onUploadAvatar: (accountId: string, file: File) => Promise<boolean>
  onUploadCover: (accountId: string, file: File) => Promise<boolean>
  onClearAccountErrors: () => void
  onAccountChanged: () => void | Promise<void>
  onDirtyChange: (hasChanges: boolean) => void
}

/** 在设置中选择并管理一个长期 AI 账号，不复用好友主页的社交展示状态。 */
export function AccountProfileSettingsPage({
  accounts,
  accountStatus,
  accountErrorMessage,
  updatingAccountId,
  updateErrorMessage,
  uploadingMedia,
  mediaErrorMessage,
  onReloadAccounts,
  onUpdateAccount,
  onUploadAvatar,
  onUploadCover,
  onClearAccountErrors,
  onAccountChanged,
  onDirtyChange,
}: AccountProfileSettingsPageProps) {
  const characterWorlds = useCharacterWorlds()
  const [searchText, setSearchText] = useState('')
  const [selectedAccountId, setSelectedAccountId] = useState<string | undefined>(
    () => getInitialAccountId() ?? accounts[0]?.id,
  )
  const [activeTab, setActiveTab] = useState<ProfileTab>(getInitialProfileTab)
  const [hasEditorChanges, setHasEditorChanges] = useState(false)
  const selectedAccount = accounts.find((account) => account.id === selectedAccountId)
  const normalizedSearch = searchText.trim().toLocaleLowerCase()
  const filteredAccounts = useMemo(
    () => accounts.filter((account) => (
      !normalizedSearch
      || account.nickname.toLocaleLowerCase().includes(normalizedSearch)
      || account.vcNumber.toLocaleLowerCase().includes(normalizedSearch)
    )),
    [accounts, normalizedSearch],
  )
  const worldUsageCounts = useMemo(() => {
    const counts = new Map<string, number>()
    for (const account of accounts) {
      counts.set(
        account.characterWorldId,
        (counts.get(account.characterWorldId) ?? 0) + 1,
      )
    }
    return counts
  }, [accounts])

  useEffect(() => {
    if (accountStatus !== 'success' || accounts.length === 0 || selectedAccount) return
    setSelectedAccountId(accounts[0].id)
  }, [accountStatus, accounts, selectedAccount])

  useEffect(() => {
    onDirtyChange(hasEditorChanges)
  }, [hasEditorChanges, onDirtyChange])

  useEffect(() => {
    if (!hasEditorChanges) return

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault()
      event.returnValue = ''
    }

    window.addEventListener('beforeunload', handleBeforeUnload)
    return () => window.removeEventListener('beforeunload', handleBeforeUnload)
  }, [hasEditorChanges])

  function confirmDiscard(): boolean {
    return !hasEditorChanges || window.confirm('当前修改尚未保存，确定要切换吗？')
  }

  function selectAccount(accountId: string) {
    if (accountId === selectedAccountId || !confirmDiscard()) return
    setSelectedAccountId(accountId)
    setHasEditorChanges(false)
    onClearAccountErrors()
    const url = new URL(window.location.href)
    url.searchParams.set('settingsAccount', accountId)
    window.history.replaceState(null, '', url)
  }

  function changeTab(tab: ProfileTab) {
    if (tab === activeTab || !confirmDiscard()) return
    setActiveTab(tab)
    setHasEditorChanges(false)
    onClearAccountErrors()
    const url = new URL(window.location.href)
    url.searchParams.set('profileTab', tab)
    window.history.replaceState(null, '', url)
  }

  async function saveAccount(request: UpdateAiAccountRequest) {
    if (!selectedAccount) return undefined
    const updatedAccount = await onUpdateAccount(selectedAccount.id, request)
    if (updatedAccount) await onAccountChanged()
    return updatedAccount
  }

  async function uploadMedia(kind: 'avatar' | 'cover', file: File) {
    if (!selectedAccount) return false
    const succeeded = kind === 'avatar'
      ? await onUploadAvatar(selectedAccount.id, file)
      : await onUploadCover(selectedAccount.id, file)
    if (succeeded) await onAccountChanged()
    return succeeded
  }

  async function refreshWorldReferences() {
    onReloadAccounts()
    await onAccountChanged()
  }

  return (
    <div className="flex h-full min-h-0 flex-col">
      <header className="shrink-0 px-6 pt-7 xl:px-8">
        <h2 className="font-display text-2xl font-semibold tracking-[-0.025em] text-foreground">账号资料编辑</h2>
        <p className="mt-1 text-sm leading-6 text-muted-foreground">管理好友的长期账号资料、个人记忆和对其他世界的认识。</p>
      </header>

      <div className="grid min-h-0 flex-1 gap-0 px-6 pt-5 pb-6 md:grid-cols-[224px_minmax(0,1fr)] xl:grid-cols-[248px_minmax(0,1fr)] xl:px-8">
        <aside className="flex min-h-0 flex-col overflow-hidden rounded-tl-xl rounded-bl-xl border border-border bg-surface" aria-label="选择账号">
          <div className="border-b border-border p-4">
            <h3 className="text-sm font-semibold text-foreground">选择账号</h3>
            <label className="relative mt-3 block">
              <span className="sr-only">搜索账号</span>
              <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
              <input
                type="search"
                name="accountProfileSearch"
                autoComplete="off"
                className="form-control pl-9"
                value={searchText}
                onChange={(event) => setSearchText(event.target.value)}
                placeholder="搜索昵称或 VC 号…"
              />
            </label>
            {accountStatus === 'success' && <p className="mt-3 text-xs text-muted-foreground">共 {accounts.length} 个账号</p>}
          </div>

          <div className="min-h-0 flex-1 overflow-y-auto p-2">
            {(accountStatus === 'idle' || accountStatus === 'loading') && <LoadingState variant="list" />}
            {accountStatus === 'error' && <ErrorState message={accountErrorMessage} onRetry={onReloadAccounts} />}
            {accountStatus === 'success' && accounts.length === 0 && (
              <EmptyState icon={UserRoundCog} title="没有可编辑的账号" description="请先从好友页寻找并添加一位朋友。" compact />
            )}
            {accountStatus === 'success' && accounts.length > 0 && filteredAccounts.length === 0 && (
              <EmptyState icon={Search} title="没有匹配账号" description="换一个昵称或 VC 号再试试。" compact />
            )}
            {accountStatus === 'success' && filteredAccounts.length > 0 && (
              <ul className="grid gap-1" aria-label="账号列表">
                {filteredAccounts.map((account) => (
                  <li key={account.id}>
                    <ListItem
                      title={account.nickname}
                      description={`VC号：${account.vcNumber}`}
                      avatarUrl={account.avatarUrl}
                      selected={account.id === selectedAccountId}
                      onSelect={() => selectAccount(account.id)}
                    />
                  </li>
                ))}
              </ul>
            )}
          </div>
        </aside>

        <section className="min-h-0 min-w-0 overflow-hidden rounded-tr-xl rounded-br-xl border border-l-0 border-border bg-surface" aria-label="账号编辑内容">
          {!selectedAccount ? (
            <EmptyState icon={UserRoundCog} title="选择一个账号" description="从左侧选择账号后，可以编辑资料和管理 AI 记忆。" />
          ) : (
            <div className="flex h-full min-h-0 flex-col">
              <div className="flex shrink-0 gap-1 border-b border-border px-5 pt-2 sm:px-6" role="tablist" aria-label="账号管理内容">
                <ProfileTabButton selected={activeTab === 'profile'} onClick={() => changeTab('profile')} icon={UserRoundCog}>账号资料</ProfileTabButton>
                <ProfileTabButton selected={activeTab === 'memories'} onClick={() => changeTab('memories')} icon={Brain}>AI 记忆</ProfileTabButton>
                <ProfileTabButton selected={activeTab === 'worldKnowledge'} onClick={() => changeTab('worldKnowledge')} icon={Globe2}>世界认知</ProfileTabButton>
              </div>
              <div className="min-h-0 flex-1 overflow-y-auto" role="tabpanel">
                {activeTab === 'profile' ? (
                  <AccountProfileEditor
                    key={selectedAccount.id}
                    account={selectedAccount}
                    isSaving={updatingAccountId === selectedAccount.id}
                    isUploadingAvatar={uploadingMedia?.accountId === selectedAccount.id && uploadingMedia.kind === 'avatar'}
                    isUploadingCover={uploadingMedia?.accountId === selectedAccount.id && uploadingMedia.kind === 'cover'}
                    saveErrorMessage={updateErrorMessage}
                    mediaErrorMessage={mediaErrorMessage}
                    characterWorlds={characterWorlds}
                    worldUsageCounts={worldUsageCounts}
                    onSave={saveAccount}
                    onUploadAvatar={(file) => uploadMedia('avatar', file)}
                    onUploadCover={(file) => uploadMedia('cover', file)}
                    onWorldChanged={refreshWorldReferences}
                    onDirtyChange={setHasEditorChanges}
                  />
                ) : activeTab === 'memories' ? (
                  <AiSelfMemoryPanel
                    key={selectedAccount.id}
                    aiAccountId={selectedAccount.id}
                    characterWorlds={characterWorlds.data}
                    defaultCharacterWorldId={selectedAccount.characterWorldId}
                    onDirtyChange={setHasEditorChanges}
                  />
                ) : (
                  <AiWorldKnowledgePanel
                    key={selectedAccount.id}
                    aiAccountId={selectedAccount.id}
                    onDirtyChange={setHasEditorChanges}
                  />
                )}
              </div>
            </div>
          )}
        </section>
      </div>
    </div>
  )
}

function ProfileTabButton({ selected, onClick, icon: Icon, children }: { selected: boolean; onClick: () => void; icon: typeof Brain; children: string }) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={selected}
      tabIndex={selected ? 0 : -1}
      onClick={onClick}
      className={cn(
        'relative flex items-center gap-2 px-3 py-3 text-sm font-medium text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset',
        selected && 'text-primary after:absolute after:inset-x-2 after:bottom-[-1px] after:h-0.5 after:bg-primary',
      )}
    >
      <Icon className="size-4" aria-hidden="true" />
      {children}
    </button>
  )
}

function getInitialAccountId(): string | undefined {
  return new URLSearchParams(window.location.search).get('settingsAccount') ?? undefined
}

function getInitialProfileTab(): ProfileTab {
  const tab = new URLSearchParams(window.location.search).get('profileTab')
  return tab === 'memories' || tab === 'worldKnowledge' ? tab : 'profile'
}
