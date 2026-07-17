import { MessagesSquare, UsersRound } from 'lucide-react'
import type { GroupChatResponse } from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { EmptyState } from '@/components/feedback/EmptyState'
import { LoadingState } from '@/components/feedback/LoadingState'
import { Badge } from '@/components/ui/badge'
import type { RemoteStatus } from '@/types/remoteStatus'
import { formatDateTime } from '@/utils/dateTime'

interface GroupChatDetailsProps {
  groupChat?: GroupChatResponse
  status: RemoteStatus
  isEmpty: boolean
}

export function GroupChatDetails({
  groupChat,
  status,
  isEmpty,
}: GroupChatDetailsProps) {
  if (status === 'idle' || status === 'loading') {
    return <LoadingState variant="detail" />
  }

  if (status === 'error') {
    return (
      <EmptyState
        icon={MessagesSquare}
        title="群聊资料暂不可用"
        description="请在群聊列表中重新加载数据。"
      />
    )
  }

  if (isEmpty) {
    return (
      <EmptyState
        icon={MessagesSquare}
        title="暂无群聊资料"
        description="创建群聊并选择已有 AI 账号后，可以在这里查看成员关系。"
      />
    )
  }

  if (!groupChat) {
    return (
      <EmptyState
        icon={MessagesSquare}
        title="选择一个群聊"
        description="从群聊列表选择一个会话，查看群聊资料和 AI 成员。"
      />
    )
  }

  return (
    <article className="mx-auto w-full max-w-3xl px-10 py-12">
      <header className="flex min-w-0 items-center gap-5 border-b border-border pb-8">
        <EntityAvatar name={groupChat.name} label="群" size="large" />
        <div className="grid min-w-0 gap-1.5">
          <p className="text-xs font-semibold tracking-[0.1em] text-primary uppercase">
            GroupChat
          </p>
          <h2 className="truncate font-display text-[28px] leading-tight font-semibold tracking-[-0.02em]">
            {groupChat.name}
          </h2>
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <UsersRound className="size-4" strokeWidth={1.75} aria-hidden="true" />
            <span>{groupChat.members.length} 位 AI 成员</span>
          </div>
        </div>
      </header>

      <section className="pt-8" aria-labelledby="group-members-heading">
        <div className="mb-3 flex items-center justify-between gap-4">
          <h3
            id="group-members-heading"
            className="font-display text-base font-semibold"
          >
            群成员
          </h3>
          <Badge>{groupChat.members.length}</Badge>
        </div>

        {groupChat.members.length === 0 ? (
          <p className="border-y border-border py-6 text-sm text-muted-foreground">
            当前群聊没有 AI 成员。
          </p>
        ) : (
          <ul className="border-y border-border">
            {groupChat.members.map((member) => (
              <li
                key={member.id}
                className="flex min-w-0 items-center gap-3 border-b border-border py-3.5 last:border-b-0"
              >
                <EntityAvatar name={member.nickname} size="small" />
                <span className="min-w-0 flex-1 truncate text-sm font-medium">
                  {member.nickname}
                </span>
                <span className="text-xs text-muted-foreground">AI 账号</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="pt-8" aria-labelledby="group-information-heading">
        <h3
          id="group-information-heading"
          className="mb-3 font-display text-base font-semibold"
        >
          群聊资料
        </h3>
        <dl className="border-y border-border">
          <div className="grid grid-cols-[112px_minmax(0,1fr)] gap-6 border-b border-border py-5">
            <dt className="text-xs font-medium tracking-[0.04em] text-muted-foreground uppercase">
              创建时间
            </dt>
            <dd className="text-sm">{formatDateTime(groupChat.createdAt)}</dd>
          </div>
          <div className="grid grid-cols-[112px_minmax(0,1fr)] gap-6 py-5">
            <dt className="text-xs font-medium tracking-[0.04em] text-muted-foreground uppercase">
              群聊 ID
            </dt>
            <dd className="break-all font-mono text-xs leading-6 text-muted-foreground">
              {groupChat.id}
            </dd>
          </div>
        </dl>
      </section>
    </article>
  )
}
