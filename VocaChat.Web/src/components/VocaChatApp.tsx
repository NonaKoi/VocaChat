import { useMemo, useState } from 'react'
import type {
  ConversationSummaryResponse,
  CreateAiAccountRequest,
  CreateGroupChatRequest,
  GroupChatResponse,
} from '@/api/types'
import { openPrivateChat } from '@/api/contacts'
import { AiAccountCreateForm } from '@/components/aiAccounts/AiAccountCreateForm'
import { AiAccountDetails } from '@/components/aiAccounts/AiAccountDetails'
import { ActivityFeed } from '@/components/activity/ActivityFeed'
import { ChatWorkspace } from '@/components/chat/ChatWorkspace'
import { ConversationList } from '@/components/chat/ConversationList'
import { ContactList } from '@/components/contacts/ContactList'
import { GroupChatCreatePanel } from '@/components/groupChats/GroupChatCreatePanel'
import { AppShell } from '@/components/layout/AppShell'
import { NavigationRail } from '@/components/layout/NavigationRail'
import { AutonomousInteractionSettingsPage } from '@/components/settings/AutonomousInteractionSettingsPage'
import { useAiAccounts } from '@/hooks/useAiAccounts'
import { useContacts } from '@/hooks/useContacts'
import { useConversations } from '@/hooks/useConversations'
import { useGroupChats } from '@/hooks/useGroupChats'
import { useGroupMessages } from '@/hooks/useGroupMessages'
import { usePosts } from '@/hooks/usePosts'
import { usePrivateMessages } from '@/hooks/usePrivateMessages'
import type { AppSection } from '@/types/appSection'

const emptyAiAccountDraft: CreateAiAccountRequest = { nickname: '', vcNumber: '', identityDescription: '', personality: '', speakingStyle: '', signature: '', birthday: '', gender: 'Unspecified', location: '', occupation: '', hometown: '', onlineStatus: 'Offline', interestTags: [], personalityTags: [] }

export function VocaChatApp() {
  const [activeSection, setActiveSection] = useState<AppSection>(getInitialSection)
  const [selectedConversation, setSelectedConversation] = useState<ConversationSummaryResponse>()
  const [selectedAccountId, setSelectedAccountId] = useState<string | undefined>(() => new URLSearchParams(window.location.search).get('friend') ?? undefined)
  const [isAccountCreateOpen, setIsAccountCreateOpen] = useState(false)
  const [isGroupChatCreateOpen, setIsGroupChatCreateOpen] = useState(false)
  const [hasUnsavedSettings, setHasUnsavedSettings] = useState(false)
  const [aiAccountDraft, setAiAccountDraft] = useState(emptyAiAccountDraft)
  const [drafts, setDrafts] = useState<Record<string, string>>({})

  const aiAccounts = useAiAccounts()
  const contacts = useContacts()
  const conversations = useConversations()
  const groupChats = useGroupChats()
  const posts = usePosts()
  const selectedContact = contacts.data.find((item) => item.friend.id === selectedAccountId)
  const selectedGroup = useMemo(() => selectedConversation?.kind === 'GroupChat' ? groupChats.data.find((item) => item.id === selectedConversation.id) : undefined, [groupChats.data, selectedConversation])
  const selectedPrivateContact = selectedConversation?.kind === 'PrivateChat' ? contacts.data.find((item) => item.id === selectedConversation.contactId) : undefined
  const groupMessages = useGroupMessages(selectedConversation?.kind === 'GroupChat' ? selectedConversation.id : undefined)
  const privateMessages = usePrivateMessages(selectedConversation?.kind === 'PrivateChat' ? selectedConversation.id : undefined)
  const messageState = selectedConversation?.kind === 'PrivateChat' ? privateMessages : groupMessages
  const conversationKey = selectedConversation ? `${selectedConversation.kind}:${selectedConversation.id}` : undefined

  function openAccountCreate() { aiAccounts.clearCreateError(); aiAccounts.clearMediaUploadError(); setIsAccountCreateOpen(true) }
  async function createAccount(request: CreateAiAccountRequest) {
    const account = await aiAccounts.create(request)
    if (account) { await contacts.reload(); selectAccount(account.id); setIsAccountCreateOpen(false); setAiAccountDraft(emptyAiAccountDraft) }
    return account
  }
  async function startPrivateChat() {
    if (!selectedContact) return
    const privateChat = await openPrivateChat(selectedContact.id)
    if (!privateChat.friend) return
    await conversations.reload()
    setSelectedConversation({ id: privateChat.id, kind: 'PrivateChat', category: 'MyPrivateChat', displayName: privateChat.friend.nickname, avatarUrl: privateChat.friend.avatarUrl, memberCount: 1, contactId: privateChat.contactId, latestSenderDisplayName: null, latestMessageContent: null, latestMessageAt: null, createdAt: privateChat.createdAt })
    setActiveSection('chat')
  }
  function openGroupChatCreate() {
    groupChats.clearCreateError()
    setIsGroupChatCreateOpen(true)
  }
  async function createGroupChat(request: CreateGroupChatRequest) {
    const createdGroupChat = await groupChats.create(request)
    if (!createdGroupChat) return undefined

    const refreshedConversations = await conversations.reload()
    const createdConversation = refreshedConversations?.find(
      (item) => item.kind === 'GroupChat' && item.id === createdGroupChat.id,
    ) ?? toGroupConversation(createdGroupChat)

    setSelectedConversation(createdConversation)
    setIsGroupChatCreateOpen(false)
    return createdGroupChat
  }
  async function addGroupChatMember(groupChatId: string, aiAccountId: string) {
    const updatedGroupChat = await groupChats.addMember(groupChatId, aiAccountId)
    if (!updatedGroupChat) return false

    const refreshedConversations = await conversations.reload()
    const updatedConversation = refreshedConversations?.find(
      (item) => item.kind === 'GroupChat' && item.id === groupChatId,
    ) ?? toGroupConversation(updatedGroupChat)
    setSelectedConversation(updatedConversation)
    return true
  }
  async function uploadMedia(kind: 'avatar' | 'cover', file: File) {
    if (!selectedContact) return
    if (kind === 'avatar') await aiAccounts.uploadAvatar(selectedContact.friend.id, file)
    else await aiAccounts.uploadCover(selectedContact.friend.id, file)
    await contacts.reload()
  }
  async function openAutonomousPrivateChat(privateChatId: string) {
    const refreshedConversations = await conversations.reload()
    const conversation = refreshedConversations?.find(
      (item) => item.kind === 'PrivateChat' && item.id === privateChatId,
    )
    if (!conversation) return

    setSelectedConversation(conversation)
    setActiveSection('chat')
    const url = new URL(window.location.href)
    url.searchParams.set('section', 'chat')
    window.history.replaceState(null, '', url)
  }
  async function openAutonomousGroupChat(groupChatId: string) {
    const [, refreshedConversations] = await Promise.all([
      groupChats.reload(),
      conversations.reload(),
    ])
    const conversation = refreshedConversations?.find(
      (item) => item.kind === 'GroupChat' && item.id === groupChatId,
    )
    if (!conversation) return

    setSelectedConversation(conversation)
    setActiveSection('chat')
    const url = new URL(window.location.href)
    url.searchParams.set('section', 'chat')
    window.history.replaceState(null, '', url)
  }
  function selectAccount(id: string) {
    setSelectedAccountId(id)
    const url = new URL(window.location.href)
    url.searchParams.set('friend', id)
    window.history.replaceState(null, '', url)
  }

  const listPanel = activeSection === 'chat'
    ? (
        <ConversationList
          conversations={conversations.data}
          status={conversations.status}
          selectedKey={conversationKey}
          errorMessage={conversations.errorMessage}
          onSelect={(conversation) => {
            setSelectedConversation(conversation)
            setIsGroupChatCreateOpen(false)
          }}
          onCreateGroupChat={openGroupChatCreate}
          onRetry={conversations.reload}
        />
      )
    : activeSection === 'friends'
      ? <ContactList contacts={contacts.data} groups={contacts.groups} status={contacts.status} selectedAccountId={selectedAccountId} isCreating={isAccountCreateOpen} errorMessage={contacts.errorMessage} onSelect={(id) => { selectAccount(id); setIsAccountCreateOpen(false); aiAccounts.clearMediaUploadError() }} onCreate={openAccountCreate} onRetry={contacts.reload} />
      : undefined

  let contentPanel
  if (activeSection === 'chat') {
    const title = selectedConversation?.displayName
    contentPanel = isGroupChatCreateOpen
      ? (
          <GroupChatCreatePanel
            contacts={contacts.data}
            contactStatus={contacts.status}
            contactErrorMessage={contacts.errorMessage}
            isSubmitting={groupChats.isCreating}
            errorMessage={groupChats.createErrorMessage}
            onRetryContacts={contacts.reload}
            onCancel={() => {
              groupChats.clearCreateError()
              setIsGroupChatCreateOpen(false)
            }}
            onCreate={createGroupChat}
          />
        )
      : (
          <ChatWorkspace
            conversationId={selectedConversation?.id}
            title={title}
            avatarUrl={selectedConversation?.avatarUrl}
            kind={selectedConversation?.kind}
            category={selectedConversation?.category}
            friend={selectedPrivateContact?.friend}
            privateChat={privateMessages.privateChat}
            groupChat={selectedGroup}
            contacts={contacts.data}
            contactStatus={contacts.status}
            messages={messageState.data}
            messageStatus={messageState.status}
            messageError={messageState.errorMessage}
            sendError={messageState.sendErrorMessage}
            isSending={messageState.isSending}
            isAddingGroupMember={groupChats.addingMemberToGroupId === selectedGroup?.id}
            groupMemberError={groupChats.memberErrorMessage}
            draft={conversationKey ? drafts[conversationKey] ?? '' : ''}
            onDraftChange={(value) => conversationKey && setDrafts(
              (current) => ({ ...current, [conversationKey]: value }),
            )}
            onReloadMessages={messageState.reload}
            onRetryContacts={contacts.reload}
            onClearGroupMemberError={groupChats.clearMemberError}
            onAddGroupMember={addGroupChatMember}
            onSendMessage={async (content) => {
              const result = await messageState.send(content)
              if (result !== 'rejected') void conversations.reload()
              return result
            }}
          />
        )
  } else if (activeSection === 'friends') {
    contentPanel = isAccountCreateOpen ? <AiAccountCreateForm values={aiAccountDraft} isSubmitting={aiAccounts.isCreating} errorMessage={aiAccounts.createErrorMessage} onValuesChange={setAiAccountDraft} onCancel={() => { setIsAccountCreateOpen(false); setAiAccountDraft(emptyAiAccountDraft) }} onCreate={createAccount} /> : <div className="h-full overflow-y-auto bg-surface-muted"><AiAccountDetails account={selectedContact?.friend} status={contacts.status} isEmpty={contacts.data.length === 0} isUploadingAvatar={aiAccounts.uploadingMedia?.accountId === selectedContact?.friend.id && aiAccounts.uploadingMedia?.kind === 'avatar'} isUploadingCover={aiAccounts.uploadingMedia?.accountId === selectedContact?.friend.id && aiAccounts.uploadingMedia?.kind === 'cover'} mediaUploadErrorMessage={aiAccounts.mediaUploadErrorMessage} onUploadAvatar={selectedContact ? (file) => uploadMedia('avatar', file) : undefined} onUploadCover={selectedContact ? (file) => uploadMedia('cover', file) : undefined} onSendMessage={selectedContact ? startPrivateChat : undefined} /></div>
  } else if (activeSection === 'activity') {
    contentPanel = <ActivityFeed posts={posts.data} status={posts.status} errorMessage={posts.errorMessage} actionError={posts.actionError} onRetry={posts.reload} onToggleLike={(id, liked) => void posts.toggleLike(id, liked)} onComment={(id, content) => void posts.addComment(id, content)} />
  } else if (activeSection === 'settings') {
    contentPanel = (
      <AutonomousInteractionSettingsPage
        contacts={contacts.data}
        contactStatus={contacts.status}
        contactErrorMessage={contacts.errorMessage}
        onReloadContacts={async () => { await contacts.reload() }}
        onDirtyChange={setHasUnsavedSettings}
        onOpenPrivateChat={openAutonomousPrivateChat}
        onOpenGroupChat={openAutonomousGroupChat}
      />
    )
  } else {
    contentPanel = <div className="grid h-full place-content-center text-sm text-muted-foreground">该功能将在后续阶段开放</div>
  }

  function changeSection(section: AppSection) {
    if (
      activeSection === 'settings'
      && section !== 'settings'
      && hasUnsavedSettings
      && !window.confirm('设置尚未保存，确定要离开吗？')
    ) {
      return
    }

    setActiveSection(section)
    const url = new URL(window.location.href)
    url.searchParams.set('section', section)
    window.history.replaceState(null, '', url)
  }

  return <AppShell navigation={<NavigationRail activeSection={activeSection} onSectionChange={changeSection} />} listPanel={listPanel} contentPanel={contentPanel} />
}

function toGroupConversation(groupChat: GroupChatResponse): ConversationSummaryResponse {
  return {
    id: groupChat.id,
    kind: 'GroupChat',
    category: groupChat.includesLocalUser ? 'MyGroupChat' : 'FriendGroupChat',
    displayName: groupChat.name,
    avatarUrl: null,
    memberCount: groupChat.members.length + (groupChat.includesLocalUser ? 1 : 0),
    contactId: null,
    latestSenderDisplayName: null,
    latestMessageContent: null,
    latestMessageAt: null,
    createdAt: groupChat.createdAt,
  }
}

function getInitialSection(): AppSection {
  const value = new URLSearchParams(window.location.search).get('section')
  return value === 'friends' || value === 'activity' || value === 'settings'
    ? value
    : 'chat'
}
