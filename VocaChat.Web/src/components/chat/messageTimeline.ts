import type { ChatMessageResponse } from '@/api/types'

const GROUP_WINDOW_IN_MILLISECONDS = 5 * 60 * 1000

export interface MessageGroup {
  kind: 'message-group'
  id: string
  senderType: ChatMessageResponse['senderType']
  senderDisplayName: string
  senderAiAccountId: string | null
  messages: ChatMessageResponse[]
}

export interface DateDivider {
  kind: 'date-divider'
  id: string
  label: string
}

export type MessageTimelineItem = MessageGroup | DateDivider

/**
 * 将按时间排序的消息整理为日期分隔和连续发送者分组。
 * 同一发送者超过五分钟后会开启新分组，避免长时间跨度的消息被误认为同一轮发言。
 */
export function buildMessageTimeline(
  messages: ChatMessageResponse[],
  now = new Date(),
): MessageTimelineItem[] {
  const timeline: MessageTimelineItem[] = []
  let previousDateKey: string | undefined
  let currentGroup: MessageGroup | undefined

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
      currentGroup = undefined
    }

    if (!currentGroup || !canAppendToGroup(currentGroup, message)) {
      currentGroup = {
        kind: 'message-group',
        id: `group-${message.id}`,
        senderType: message.senderType,
        senderDisplayName: message.senderDisplayName,
        senderAiAccountId: message.senderAiAccountId,
        messages: [message],
      }
      timeline.push(currentGroup)
      continue
    }

    currentGroup.messages.push(message)
  }

  return timeline
}

function canAppendToGroup(
  group: MessageGroup,
  message: ChatMessageResponse,
): boolean {
  const previousMessage = group.messages.at(-1)

  if (!previousMessage) {
    return false
  }

  const sameSender =
    group.senderType === message.senderType &&
    group.senderAiAccountId === message.senderAiAccountId &&
    group.senderDisplayName === message.senderDisplayName
  const elapsedTime =
    new Date(message.sentAt).getTime() -
    new Date(previousMessage.sentAt).getTime()

  return (
    sameSender &&
    elapsedTime >= 0 &&
    elapsedTime <= GROUP_WINDOW_IN_MILLISECONDS
  )
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
