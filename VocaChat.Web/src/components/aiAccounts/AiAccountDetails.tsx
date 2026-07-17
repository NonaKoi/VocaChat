import { CircleUserRound } from 'lucide-react'
import type { AiAccountResponse } from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { EmptyState } from '@/components/feedback/EmptyState'
import { LoadingState } from '@/components/feedback/LoadingState'
import type { RemoteStatus } from '@/types/remoteStatus'
import { formatDateTime } from '@/utils/dateTime'

interface AiAccountDetailsProps {
  account?: AiAccountResponse
  status: RemoteStatus
  isEmpty: boolean
}

interface DetailRowProps {
  label: string
  value: string
  identifier?: boolean
}

function DetailRow({ label, value, identifier = false }: DetailRowProps) {
  return (
    <div className="grid grid-cols-[112px_minmax(0,1fr)] gap-6 border-b border-border py-5 last:border-b-0">
      <dt className="text-xs font-medium tracking-[0.04em] text-muted-foreground uppercase">
        {label}
      </dt>
      <dd
        className={
          identifier
            ? 'break-all font-mono text-xs leading-6 text-muted-foreground'
            : 'whitespace-pre-wrap text-sm leading-6 text-foreground'
        }
      >
        {value}
      </dd>
    </div>
  )
}

export function AiAccountDetails({
  account,
  status,
  isEmpty,
}: AiAccountDetailsProps) {
  if (status === 'idle' || status === 'loading') {
    return <LoadingState variant="detail" />
  }

  if (status === 'error') {
    return (
      <EmptyState
        icon={CircleUserRound}
        title="账号资料暂不可用"
        description="请在账号列表中重新加载数据。"
      />
    )
  }

  if (isEmpty) {
    return (
      <EmptyState
        icon={CircleUserRound}
        title="暂无账号资料"
        description="创建 AI 账号后，可以在这里查看它的身份、性格和说话风格。"
      />
    )
  }

  if (!account) {
    return (
      <EmptyState
        icon={CircleUserRound}
        title="选择一个 AI 账号"
        description="从账号列表选择一个长期 AI 身份，查看它的完整资料。"
      />
    )
  }

  return (
    <article className="mx-auto w-full max-w-3xl px-10 py-12">
      <header className="flex min-w-0 items-center gap-5 border-b border-border pb-8">
        <EntityAvatar name={account.nickname} size="large" />
        <div className="grid min-w-0 gap-1.5">
          <p className="text-xs font-semibold tracking-[0.1em] text-primary uppercase">
            AI 账号
          </p>
          <h2 className="truncate font-display text-[28px] leading-tight font-semibold tracking-[-0.02em]">
            {account.nickname}
          </h2>
          <p className="max-w-2xl text-sm leading-6 text-muted-foreground">
            {account.identityDescription || '未填写身份描述'}
          </p>
        </div>
      </header>

      <section className="pt-8" aria-labelledby="ai-identity-heading">
        <div className="mb-3 flex items-baseline justify-between gap-4">
          <h3
            id="ai-identity-heading"
            className="font-display text-base font-semibold"
          >
            身份资料
          </h3>
          <span className="text-xs text-muted-foreground">长期保存在本地</span>
        </div>
        <dl className="border-y border-border">
          <DetailRow label="性格" value={account.personality || '未填写'} />
          <DetailRow
            label="说话风格"
            value={account.speakingStyle || '未填写'}
          />
          <DetailRow label="创建时间" value={formatDateTime(account.createdAt)} />
          <DetailRow label="账号 ID" value={account.id} identifier />
        </dl>
      </section>
    </article>
  )
}
