import type { ChatMessageResponse } from '@/api/types'

export interface MessageTimelineMessage {
  kind: 'message'
  id: string
  message: ChatMessageResponse
}

export interface DateDivider {
  kind: 'date-divider'
  id: string
  label: string
}

export type MessageTimelineItem = MessageTimelineMessage | DateDivider

/**
 * 将按时间排序的消息整理为日期分隔和独立消息项。
 * 每条消息都保留自己的发送者、时间和气泡，不合并连续发言。
 */
export function buildMessageTimeline(
  messages: ChatMessageResponse[],
  now = new Date(),
): MessageTimelineItem[] {
  const timeline: MessageTimelineItem[] = []
  let previousDateKey: string | undefined

  for (const message of messages) {
    const sentAt = new Date(message.sentAt)
    const dateKey = getLocalDateKey(sentAt)

    if (dateKey !== previousDateKey) {
      timeline.push({
        kind: 'date-divider',
        id: `date-${dateKey}`,
        label: formatTimelineDate(sentAt, now),
      })
      previousDateKey = dateKey
    }

    timeline.push({
      kind: 'message',
      id: `message-${message.id}`,
      message,
    })
  }

  return timeline
}

function getLocalDateKey(date: Date): string {
  if (Number.isNaN(date.getTime())) {
    return 'invalid-date'
  }

  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function formatTimelineDate(date: Date, now: Date): string {
  if (Number.isNaN(date.getTime())) {
    return '未知日期'
  }

  const today = startOfDay(now)
  const target = startOfDay(date)
  const dayDifference = Math.round(
    (today.getTime() - target.getTime()) / (24 * 60 * 60 * 1000),
  )

  if (dayDifference === 0) {
    return '今天'
  }

  if (dayDifference === 1) {
    return '昨天'
  }

  return new Intl.DateTimeFormat('zh-CN', {
    year: date.getFullYear() === now.getFullYear() ? undefined : 'numeric',
    month: 'long',
    day: 'numeric',
  }).format(date)
}

function startOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate())
}
