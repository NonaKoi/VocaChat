import { MessagesSquare } from 'lucide-react'
import type { GroupChatResponse } from '@/api/types'
import { ListItem } from '@/components/common/ListItem'
import { Panel } from '@/components/common/Panel'
import { EmptyState } from '@/components/feedback/EmptyState'
import { ErrorState } from '@/components/feedback/ErrorState'
import { LoadingState } from '@/components/feedback/LoadingState'
import { Badge } from '@/components/ui/badge'
import type { RemoteStatus } from '@/types/remoteStatus'

interface GroupChatListProps {
  groupChats: GroupChatResponse[]
  status: RemoteStatus
  selectedId?: string
  errorMessage?: string
  onSelect: (id: string) => void
  onRetry: () => void
}

export function GroupChatList({
  groupChats,
  status,
  selectedId,
  errorMessage,
  onSelect,
  onRetry,
}: GroupChatListProps) {
  return (
    <Panel aria-labelledby="group-chat-list-title">
      <header className="flex min-h-28 items-center justify-between border-b border-border px-6 py-5">
        <div className="grid gap-1">
          <p className="text-xs font-medium tracking-[0.08em] text-muted-foreground uppercase">
            多身份会话
          </p>
          <h1
            id="group-chat-list-title"
            className="font-display text-xl font-semibold tracking-[-0.01em]"
          >
            群聊
          </h1>
        </div>
        {status === 'success' && <Badge>{groupChats.length}</Badge>}
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto">
        {(status === 'idle' || status === 'loading') && (
          <LoadingState variant="list" />
        )}
        {status === 'error' && (
          <ErrorState message={errorMessage} onRetry={onRetry} />
        )}
        {status === 'success' && groupChats.length === 0 && (
          <EmptyState
            icon={MessagesSquare}
            title="还没有群聊"
            description="创建的群聊会显示在这里。"
            compact
          />
        )}
        {status === 'success' && groupChats.length > 0 && (
          <ul className="grid gap-1.5 p-2" aria-label="群聊列表">
            {groupChats.map((groupChat) => (
              <li key={groupChat.id}>
                <ListItem
                  title={groupChat.name}
                  description={`${groupChat.members.length} 位好友`}
                  avatarLabel="群"
                  selected={selectedId === groupChat.id}
                  onSelect={() => onSelect(groupChat.id)}
                />
              </li>
            ))}
          </ul>
        )}
      </div>

      <footer className="border-t border-border px-6 py-3 text-xs text-muted-foreground">
        群成员来自已经添加的好友
      </footer>
    </Panel>
  )
}
