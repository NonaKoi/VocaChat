import type { AiReplyDelayMode } from '@/api/types'

const secondsFormatter = new Intl.NumberFormat('zh-CN', {
  maximumFractionDigits: 3,
})

export function millisecondsToSeconds(milliseconds: number): number {
  return milliseconds / 1000
}

export function secondsToMilliseconds(value: string): number {
  return Math.round(Number(value) * 1000)
}

export function isReplyTimingValid(values: {
  fixedReplyDelayMilliseconds: number
  minimumReplyDelayMilliseconds: number
  maximumReplyDelayMilliseconds: number
}): boolean {
  return Number.isSafeInteger(values.fixedReplyDelayMilliseconds)
    && Number.isSafeInteger(values.minimumReplyDelayMilliseconds)
    && Number.isSafeInteger(values.maximumReplyDelayMilliseconds)
    && values.fixedReplyDelayMilliseconds >= 0
    && values.minimumReplyDelayMilliseconds >= 0
    && values.maximumReplyDelayMilliseconds >= 0
    && values.minimumReplyDelayMilliseconds <= values.maximumReplyDelayMilliseconds
}

export function formatReplyTiming(values: {
  replyDelayMode: AiReplyDelayMode
  fixedReplyDelayMilliseconds: number
  minimumReplyDelayMilliseconds: number
  maximumReplyDelayMilliseconds: number
}): string {
  return values.replyDelayMode === 'Fixed'
    ? `${secondsFormatter.format(millisecondsToSeconds(values.fixedReplyDelayMilliseconds))} 秒`
    : `${secondsFormatter.format(millisecondsToSeconds(values.minimumReplyDelayMilliseconds))}–${secondsFormatter.format(millisecondsToSeconds(values.maximumReplyDelayMilliseconds))} 秒`
}
