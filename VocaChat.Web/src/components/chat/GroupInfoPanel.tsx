import { CalendarDays, UserRound, X } from 'lucide-react'
import type { GroupChatResponse } from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { Button } from '@/components/ui/button'
import { formatDateTime } from '@/utils/dateTime'

interface GroupInfoPanelProps {
  groupChat: GroupChatResponse
  onClose: () => void
}

/** 显示当前群聊的持久化资料和成员，不在前端虚构在线状态等数据。 */
export function GroupInfoPanel({ groupChat, onClose }: GroupInfoPanelProps) {
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
              {groupChat.members.length} 位好友
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
            <span className="text-xs tabular-nums text-muted-foreground">
              {groupChat.members.length}
            </span>
          </div>

          {groupChat.members.length === 0 ? (
            <div className="mt-5 rounded-lg border border-dashed border-border px-3 py-5 text-center">
              <UserRound
                className="mx-auto size-5 text-muted-foreground"
                aria-hidden="true"
              />
              <p className="mt-2 text-xs text-muted-foreground">暂无好友</p>
            </div>
          ) : (
            <ul className="mt-3 grid gap-1">
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
