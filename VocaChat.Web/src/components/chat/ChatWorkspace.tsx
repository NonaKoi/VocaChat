import {
  Bell,
  MessageCircleMore,
  PanelRightClose,
  PanelRightOpen,
  RotateCcw,
  UserRound,
  UsersRound,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import type {
  AiAccountResponse,
  ChatMessageResponse,
  ContactResponse,
  ConversationCategory,
  ConversationKind,
  GroupChatResponse,
  PrivateChatResponse,
} from '@/api/types'
import { GroupInfoPanel } from '@/components/chat/GroupInfoPanel'
import { MessageComposer } from '@/components/chat/MessageComposer'
import { MessageList } from '@/components/chat/MessageList'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { EmptyState } from '@/components/feedback/EmptyState'
import { LoadingState } from '@/components/feedback/LoadingState'
import { Button } from '@/components/ui/button'
import type { MessageSendOutcome } from '@/types/messageSendOutcome'
import type { RemoteStatus } from '@/types/remoteStatus'

interface Props {
  conversationId?: string
  title?: string
  avatarUrl?: string | null
  kind?: ConversationKind
  category?: ConversationCategory
  friend?: AiAccountResponse
  privateChat?: PrivateChatResponse
  groupChat?: GroupChatResponse
  contacts: ContactResponse[]
  contactStatus: RemoteStatus
  messages: ChatMessageResponse[]
  messageStatus: RemoteStatus
  messageError?: string
  sendError?: string
  isSending: boolean
  isAddingGroupMember: boolean
  groupMemberError?: string
  draft: string
  onDraftChange: (value: string) => void
  onReloadMessages: () => void
  onRetryContacts: () => void
  onClearGroupMemberError: () => void
  onAddGroupMember: (groupChatId: string, aiAccountId: string) => Promise<boolean>
  onSendMessage: (content: string) => Promise<MessageSendOutcome>
}

export function ChatWorkspace(props: Props) {
  const [infoOpen, setInfoOpen] = useState(false)
  useEffect(() => setInfoOpen(false), [props.conversationId])

  const selected = Boolean(props.conversationId)
  const canLocalUserSend = props.category === 'MyPrivateChat'
    || props.category === 'MyGroupChat'
  const rightAlignedAiAccountId = props.category === 'FriendPrivateChat'
    ? props.privateChat?.participants[1]?.id
    : undefined

  return (
    <section
      className="flex h-full min-h-0 flex-col bg-surface-muted"
      aria-label="聊天工作区"
    >
      <header className="flex h-[72px] shrink-0 items-center border-b border-border bg-surface px-6 pr-32">
        {selected && (
          <EntityAvatar
            name={props.title ?? ''}
            src={props.avatarUrl}
            size="small"
            className="mr-3"
          />
        )}
        <div className="min-w-0 flex-1">
          <h1 className="truncate text-base font-semibold">
            {props.title ?? '聊天'}
          </h1>
          <ConversationMetadata {...props} />
        </div>
        <Button variant="ghost" size="icon" disabled aria-label="通知">
          <Bell className="size-[19px]" aria-hidden="true" />
        </Button>
        {props.kind === 'GroupChat' && (
          <Button
            variant="ghost"
            size="icon"
            aria-label={infoOpen ? '关闭群聊资料' : '打开群聊资料'}
            aria-pressed={infoOpen}
            onClick={() => setInfoOpen(!infoOpen)}
          >
            {infoOpen
              ? <PanelRightClose className="size-[19px]" />
              : <PanelRightOpen className="size-[19px]" />}
          </Button>
        )}
      </header>

      {!selected && (
        <EmptyState
          icon={MessageCircleMore}
          title="选择一个聊天对象"
          description="从左侧会话列表进入私信或群聊。"
        />
      )}

      {selected
        && (props.messageStatus === 'idle' || props.messageStatus === 'loading')
        && (
          <div className="min-h-0 flex-1">
            <LoadingState variant="detail" />
          </div>
        )}

      {selected && props.messageStatus === 'error' && (
        <div className="grid min-h-0 flex-1 place-content-center text-center">
          <UserRound className="mx-auto size-8 text-muted-foreground" />
          <h2 className="mt-3 font-semibold">聊天记录暂时无法加载</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {props.messageError}
          </p>
          <Button
            variant="outline"
            className="mx-auto mt-4"
            onClick={props.onReloadMessages}
          >
            <RotateCcw className="size-4" />
            重新加载
          </Button>
        </div>
      )}

      {selected && props.messageStatus === 'success' && (
        <div className="relative flex min-h-0 flex-1">
          <div className="flex min-w-0 flex-1 flex-col">
            {props.sendError && (
              <div
                role="status"
                className="border-b border-destructive/15 bg-danger-soft px-6 py-2.5 text-sm text-destructive"
              >
                {props.sendError}
              </div>
            )}
            <MessageList
              conversationId={props.conversationId!}
              messages={props.messages}
              rightAlignedAiAccountId={rightAlignedAiAccountId}
            />
            {canLocalUserSend ? (
              <MessageComposer
                value={props.draft}
                onValueChange={props.onDraftChange}
                disabled={props.kind === 'GroupChat'
                  && props.groupChat?.members.length === 0}
                isSending={props.isSending}
                onSend={props.onSendMessage}
              />
            ) : (
              <div className="border-t border-border bg-surface px-6 py-4 text-center">
                <p className="text-sm font-medium text-foreground">
                  你不在这段会话中
                </p>
                <p className="mt-1 text-xs text-muted-foreground">
                  这是两位好友之间的私信，你可以查看他们的交流记录。
                </p>
              </div>
            )}
          </div>
          {infoOpen && props.groupChat && (
            <GroupInfoPanel
              groupChat={props.groupChat}
              contacts={props.contacts}
              contactStatus={props.contactStatus}
              isAddingMember={props.isAddingGroupMember}
              memberErrorMessage={props.groupMemberError}
              onClose={() => setInfoOpen(false)}
              onRetryContacts={props.onRetryContacts}
              onClearMemberError={props.onClearGroupMemberError}
              onAddMember={(aiAccountId) => props.onAddGroupMember(
                props.groupChat!.id,
                aiAccountId,
              )}
            />
          )}
        </div>
      )}
    </section>
  )
}

function ConversationMetadata(props: Props) {
  if (props.kind === 'GroupChat' && props.groupChat) {
    return (
      <p className="mt-0.5 flex items-center gap-1 text-xs text-muted-foreground">
        <UsersRound className="size-3.5" aria-hidden="true" />
        {props.groupChat.members.length + (props.groupChat.includesLocalUser ? 1 : 0)} 位成员
        {!props.groupChat.includesLocalUser && ' · 仅好友成员'}
      </p>
    )
  }

  if (props.category === 'FriendPrivateChat') {
    return (
      <p className="mt-0.5 text-xs text-muted-foreground">
        两位好友之间的私信
      </p>
    )
  }

  if (props.kind === 'PrivateChat' && props.friend) {
    return (
      <p className="mt-0.5 text-xs text-muted-foreground">
        {props.friend.signature || `VC号：${props.friend.vcNumber}`}
      </p>
    )
  }

  return null
}
