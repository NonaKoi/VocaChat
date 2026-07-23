import type {
  AiMessageTokenUsageResponse,
  AiModelStageTokenUsageResponse,
} from '@/api/types'
import { cn } from '@/lib/utils'

interface MessageTokenUsageProps {
  usage: AiMessageTokenUsageResponse | null | undefined
  align: 'left' | 'right'
}

/**
 * 显示一次 AI 回复关联的真实模型调用用量。
 * 多条气泡共享同一次调用时只声明共享关系，不进行虚假的逐条分摊。
 */
export function MessageTokenUsage({
  usage,
  align,
}: MessageTokenUsageProps) {
  if (!usage) {
    return (
      <p
        className={cn(
          'px-1 text-[11px] leading-4 text-muted-foreground',
          align === 'right' && 'text-right',
        )}
      >
        Token 用量未记录
      </p>
    )
  }

  const responseSharedLabel = usage.responseSharedMessageCount > 1
    ? `本批 ${usage.responseSharedMessageCount} 条共享`
    : undefined
  const groupSharedLabel = usage.interactionSharedMessageCount > 1
    ? `本轮 ${usage.interactionSharedMessageCount} 条共享`
    : undefined

  return (
    <div
      className={cn(
        'grid gap-0.5 px-1 text-[11px] leading-4 text-muted-foreground tabular-nums',
        align === 'right' && 'text-right',
      )}
      aria-label="Token 消耗明细"
    >
      <StageUsage
        label="群级导演"
        sharedLabel={groupSharedLabel}
        usage={usage.groupDirector}
      />
      <StageUsage
        label="单人导演"
        sharedLabel={responseSharedLabel}
        usage={usage.conversationDirector}
      />
      <StageUsage
        label="回复生成"
        sharedLabel={responseSharedLabel}
        usage={usage.replyGeneration}
      />
      <p>
        合计 {formatTokenCount(usage.totalTokens)} Token
        {!usage.usageComplete && ' · 部分调用未返回用量'}
      </p>
    </div>
  )
}

function StageUsage({
  label,
  sharedLabel,
  usage,
}: {
  label: string
  sharedLabel?: string
  usage: AiModelStageTokenUsageResponse | null
}) {
  if (!usage) return null

  return (
    <p>
      {label}
      {sharedLabel && `（${sharedLabel}）`}
      {' · '}
      入 {formatTokenCount(usage.inputTokens)}
      {' / '}
      出 {formatTokenCount(usage.outputTokens)}
      {' / '}
      共 {formatTokenCount(usage.totalTokens)}
      {usage.reasoningTokens !== null
        && `（推理 ${formatTokenCount(usage.reasoningTokens)}）`}
      {(usage.cacheHitTokens !== null || usage.cacheMissTokens !== null)
        && ` · 缓存命中 ${formatTokenCount(usage.cacheHitTokens)} / 未命中 ${formatTokenCount(usage.cacheMissTokens)}`}
      {usage.attemptCount > 1 && ` · ${usage.attemptCount} 次调用`}
      {!usage.usageComplete && ' · 数据不完整'}
    </p>
  )
}

function formatTokenCount(value: number | null): string {
  return value === null ? '—' : value.toLocaleString('zh-CN')
}
