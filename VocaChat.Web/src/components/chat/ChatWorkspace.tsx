import { useEffect, useState } from 'react'
import {
  Bell,
  MessageCircleMore,
  Minus,
  PanelRightClose,
  PanelRightOpen,
  RotateCcw,
  Square,
  UsersRound,
  X,
} from 'lucide-react'
import type { GroupChatResponse, GroupMessageResponse } from '@/api/types'
import { EmptyState } from '@/components/feedback/EmptyState'
import { LoadingState } from '@/components/feedback/LoadingState'
import { GroupInfoPanel } from '@/components/chat/GroupInfoPanel'
import { MessageComposer } from '@/components/chat/MessageComposer'
import { MessageList } from '@/components/chat/MessageList'
import { Button } from '@/components/ui/button'
import type { MessageSendOutcome } from '@/hooks/useGroupMessages'
import type { RemoteStatus } from '@/types/remoteStatus'

interface ChatWorkspaceProps {
  groupChat?: GroupChatResponse
  messages: GroupMessageResponse[]
  messageStatus: RemoteStatus
  messageError?: string
  sendError?: string
  isSending: boolean
  draft: string
  onDraftChange: (value: string) => void
  onReloadMessages: () => void
  onSendMessage: (content: string) => Promise<MessageSendOutcome>
}

/** 聊天页主工作区：会话标题、消息历史、群资料和输入区域。 */
export function ChatWorkspace({
  groupChat,
  messages,
  messageStatus,
  messageError,
  sendError,
  isSending,
  draft,
  onDraftChange,
  onReloadMessages,
  onSendMessage,
}: ChatWorkspaceProps) {
  const [isGroupInfoOpen, setIsGroupInfoOpen] = useState(false)

  useEffect(() => {
    setIsGroupInfoOpen(false)
  }, [groupChat?.id])

  return (
    <section
      className="flex h-full min-h-0 flex-col bg-surface-muted"
      aria-label="聊天工作区"
    >
      <WorkspaceHeader
        groupChat={groupChat}
        isGroupInfoOpen={isGroupInfoOpen}
        onToggleGroupInfo={() => setIsGroupInfoOpen((current) => !current)}
      />

      {!groupChat && (
        <EmptyState
          icon={MessageCircleMore}
          title="选择一个聊天对象"
          description="从左侧会话列表选择一个群聊，开始你的对话。"
        />
      )}

      {groupChat && (messageStatus === 'idle' || messageStatus === 'loading') && (
        <div className="min-h-0 flex-1 overflow-hidden">
          <LoadingState variant="detail" />
        </div>
      )}

      {groupChat && messageStatus === 'error' && (
        <div className="grid min-h-0 flex-1 place-content-center px-8 text-center">
          <MessageCircleMore
            className="mx-auto size-8 text-muted-foreground"
            strokeWidth={1.5}
            aria-hidden="true"
          />
          <h2 className="mt-3 text-base font-semibold">聊天记录暂时无法加载</h2>
          <p className="mt-1 max-w-md text-sm leading-6 text-muted-foreground">
            {messageError ?? '请确认 Web API 已启动，然后重新加载聊天记录。'}
          </p>
          <Button
            variant="outline"
            className="mx-auto mt-4"
            onClick={onReloadMessages}
          >
            <RotateCcw className="size-4" aria-hidden="true" />
            重新加载
          </Button>
        </div>
      )}

      {groupChat && messageStatus === 'success' && (
        <div className="relative flex min-h-0 flex-1">
          <div className="flex min-w-0 flex-1 flex-col">
            {sendError && (
              <div
                className="border-b border-destructive/15 bg-danger-soft px-6 py-2.5 text-sm text-destructive"
                role="status"
              >
                {sendError}
              </div>
            )}
            <MessageList groupChatId={groupChat.id} messages={messages} />
            <MessageComposer
              value={draft}
              onValueChange={onDraftChange}
              disabled={groupChat.members.length === 0}
              disabledReason={
                groupChat.members.length === 0
                  ? '当前群聊没有 AI 成员，暂时无法发送消息'
                  : undefined
              }
              isSending={isSending}
              onSend={onSendMessage}
            />
          </div>

          {isGroupInfoOpen && (
            <GroupInfoPanel
              groupChat={groupChat}
              onClose={() => setIsGroupInfoOpen(false)}
            />
          )}
        </div>
      )}
    </section>
  )
}

interface WorkspaceHeaderProps {
  groupChat?: GroupChatResponse
  isGroupInfoOpen: boolean
  onToggleGroupInfo: () => void
}

function WorkspaceHeader({
  groupChat,
  isGroupInfoOpen,
  onToggleGroupInfo,
}: WorkspaceHeaderProps) {
  const GroupInfoIcon = isGroupInfoOpen ? PanelRightClose : PanelRightOpen

  return (
    <header className="flex h-[72px] shrink-0 items-center border-b border-border bg-surface px-5 xl:px-7">
      <div className="min-w-0 flex-1">
        <h1 className="truncate text-base font-semibold text-foreground">
          {groupChat?.name ?? '聊天'}
        </h1>
        {groupChat && (
          <p className="mt-0.5 flex items-center gap-1.5 text-xs text-muted-foreground">
            <UsersRound className="size-3.5" aria-hidden="true" />
            {groupChat.members.length} 位 AI 成员
          </p>
        )}
      </div>

      <div className="flex items-center gap-1" role="group" aria-label="聊天操作">
        <Button
          variant="ghost"
          size="icon"
          disabled
          aria-label="通知，后续开放"
          title="通知功能将在后续阶段开放"
        >
          <Bell className="size-[19px]" strokeWidth={1.7} aria-hidden="true" />
        </Button>
        <Button
          variant="ghost"
          size="icon"
          disabled={!groupChat}
          aria-pressed={isGroupInfoOpen}
          aria-label={isGroupInfoOpen ? '关闭群聊资料' : '打开群聊资料'}
          title={isGroupInfoOpen ? '关闭群聊资料' : '打开群聊资料'}
          onClick={onToggleGroupInfo}
        >
          <GroupInfoIcon className="size-[19px]" strokeWidth={1.7} aria-hidden="true" />
        </Button>
      </div>

      <div
        className="ml-3 hidden items-center gap-1 border-l border-border pl-3 xl:flex"
        aria-hidden="true"
      >
        {[
          { name: 'minimize', icon: Minus },
          { name: 'maximize', icon: Square },
          { name: 'close', icon: X },
        ].map(({ name, icon: Icon }) => (
          <span
            key={name}
            className="grid size-9 place-items-center text-muted-foreground"
          >
            <Icon className="size-4" strokeWidth={1.6} />
          </span>
        ))}
      </div>
    </header>
  )
}
