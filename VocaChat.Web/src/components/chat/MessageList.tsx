import { useEffect, useMemo, useRef, useState, type UIEvent } from 'react'
import { ArrowDown } from 'lucide-react'
import type { DisplayChatMessage } from '@/types/displayChatMessage'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { Button } from '@/components/ui/button'
import {
  buildMessageTimeline,
} from '@/components/chat/messageTimeline'
import { formatMessageTime } from '@/utils/dateTime'

const BOTTOM_THRESHOLD = 96

interface MessageListProps {
  conversationId: string
  messages: DisplayChatMessage[]
  rightAlignedAiAccountId?: string
}

/**
 * 显示逐条消息时间线。用户阅读较早消息时不会被新消息强制拉回底部。
 */
export function MessageList({
  conversationId,
  messages,
  rightAlignedAiAccountId,
}: MessageListProps) {
  const scrollContainerRef = useRef<HTMLDivElement>(null)
  const previousMessageCountRef = useRef(0)
  const isNearBottomRef = useRef(true)
  const [pendingMessageCount, setPendingMessageCount] = useState(0)
  const timeline = useMemo(() => buildMessageTimeline(messages), [messages])

  useEffect(() => {
    previousMessageCountRef.current = 0
    isNearBottomRef.current = true
    setPendingMessageCount(0)

    requestAnimationFrame(() => scrollToBottom('auto'))
  }, [conversationId])

  useEffect(() => {
    const addedCount = messages.length - previousMessageCountRef.current
    previousMessageCountRef.current = messages.length

    if (addedCount <= 0) {
      return
    }

    if (isNearBottomRef.current) {
      requestAnimationFrame(() => scrollToBottom('smooth'))
      return
    }

    setPendingMessageCount((current) => current + addedCount)
  }, [messages])

  function handleScroll(event: UIEvent<HTMLDivElement>): void {
    const container = event.currentTarget
    const distanceFromBottom =
      container.scrollHeight - container.scrollTop - container.clientHeight
    const isNearBottom = distanceFromBottom <= BOTTOM_THRESHOLD

    isNearBottomRef.current = isNearBottom
    if (isNearBottom) {
      setPendingMessageCount(0)
    }
  }

  function scrollToBottom(behavior: ScrollBehavior): void {
    const container = scrollContainerRef.current
    if (!container) {
      return
    }

    container.scrollTo({ top: container.scrollHeight, behavior })
    isNearBottomRef.current = true
    setPendingMessageCount(0)
  }

  return (
    <div className="relative min-h-0 flex-1 bg-chat">
      <div
        ref={scrollContainerRef}
        className="h-full overflow-y-auto px-6 py-7 xl:px-8 2xl:px-10"
        role="log"
        aria-live="polite"
        aria-label="聊天记录"
        onScroll={handleScroll}
      >
        {messages.length === 0 ? (
          <div className="grid h-full min-h-56 place-content-center text-center">
            <p className="text-sm font-medium text-foreground">还没有聊天记录</p>
            <p className="mt-1 text-sm text-muted-foreground">
              发送第一条消息，群内好友会给出回复。
            </p>
          </div>
        ) : (
          <ol className="mx-auto grid w-full max-w-4xl gap-5">
            {timeline.map((item) =>
              item.kind === 'date-divider' ? (
                <li key={item.id} className="flex items-center gap-3 py-1" aria-label={item.label}>
                  <span className="h-px flex-1 bg-border/70" />
                  <time className="text-[11px] font-medium text-muted-foreground">
                    {item.label}
                  </time>
                  <span className="h-px flex-1 bg-border/70" />
                </li>
              ) : (
                <MessageItem
                  key={item.id}
                  message={item.message}
                  rightAlignedAiAccountId={rightAlignedAiAccountId}
                />
              ),
            )}
          </ol>
        )}
      </div>

      {pendingMessageCount > 0 && (
        <Button
          variant="outline"
          className="absolute right-5 bottom-4 h-9 rounded-full bg-surface px-3 shadow-message"
          onClick={() => scrollToBottom('smooth')}
          aria-label={`回到最新消息，有 ${pendingMessageCount} 条新消息`}
        >
          <ArrowDown className="size-4" aria-hidden="true" />
          {pendingMessageCount} 条新消息
        </Button>
      )}
    </div>
  )
}

interface MessageItemProps {
  message: DisplayChatMessage
  rightAlignedAiAccountId?: string
}

function MessageItem({ message, rightAlignedAiAccountId }: MessageItemProps) {
  const isLocalUser = message.senderType === 'User'
  const isRightAlignedAi =
    message.senderType === 'AiAccount'
    && message.senderAiAccountId === rightAlignedAiAccountId
  const isRightAligned = isLocalUser || isRightAlignedAi

  return (
    <li className={isRightAligned ? 'flex justify-end' : 'flex justify-start'}>
      <article
        className={
          isRightAligned
            ? 'flex max-w-[78%] flex-row-reverse items-start gap-3'
            : 'flex max-w-[78%] items-start gap-3'
        }
      >
        {!isLocalUser && (
          <EntityAvatar
            name={message.senderDisplayName}
            src={message.senderAvatarUrl}
            size="small"
            className="mt-5 shrink-0"
          />
        )}
        <div className={isRightAligned ? 'grid justify-items-end gap-1.5' : 'grid gap-1.5'}>
          <div className="flex items-center gap-2 px-1 text-xs text-muted-foreground">
            {!isLocalUser && <span>{message.senderDisplayName}</span>}
            <time dateTime={message.sentAt}>
              {formatMessageTime(message.sentAt)}
            </time>
            {message.deliveryStatus === 'Sending' && (
              <span role="status">发送中</span>
            )}
          </div>

          <p
            className={
              isLocalUser
                ? `break-words whitespace-pre-wrap rounded-xl rounded-tr-sm bg-primary px-4 py-2.5 text-sm leading-6 text-white shadow-message ${message.deliveryStatus === 'Sending' ? 'opacity-70' : ''}`
                : isRightAlignedAi
                  ? 'break-words whitespace-pre-wrap rounded-xl rounded-tr-sm bg-primary-soft px-4 py-2.5 text-sm leading-6 text-foreground shadow-message'
                  : 'break-words whitespace-pre-wrap rounded-xl rounded-tl-sm bg-surface px-4 py-2.5 text-sm leading-6 text-foreground shadow-message'
            }
          >
            {message.content}
          </p>
        </div>
      </article>
    </li>
  )
}
