import {
  BriefcaseBusiness,
  CakeSlice,
  CalendarClock,
  CircleUserRound,
  House,
  MapPin,
  MessageCircleMore,
  Quote,
  Sparkles,
  Star,
  VenusAndMars,
} from 'lucide-react'
import type { AiAccountGender, AiAccountResponse, OnlineStatus } from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { ProfileMediaUploadButton } from '@/components/aiAccounts/ProfileMediaUploadButton'
import { Button } from '@/components/ui/button'
import { EmptyState } from '@/components/feedback/EmptyState'
import { LoadingState } from '@/components/feedback/LoadingState'
import type { RemoteStatus } from '@/types/remoteStatus'

interface AiAccountDetailsProps {
  account?: AiAccountResponse
  status: RemoteStatus
  isEmpty: boolean
  isUploadingAvatar?: boolean
  isUploadingCover?: boolean
  mediaUploadErrorMessage?: string
  onUploadAvatar?: (file: File) => Promise<unknown>
  onUploadCover?: (file: File) => Promise<unknown>
  onSendMessage?: () => void
}

const genderLabels: Record<AiAccountGender, string> = {
  Unspecified: '未设置',
  Male: '男',
  Female: '女',
  Other: '其他',
}

const onlineStatusPresentation: Record<
  OnlineStatus,
  { label: string; className: string }
> = {
  Offline: { label: '离线', className: 'bg-muted-foreground/45' },
  Online: { label: '在线', className: 'bg-success' },
  Away: { label: '离开', className: 'bg-warning' },
  Busy: { label: '忙碌', className: 'bg-destructive' },
}

export function AiAccountDetails({
  account,
  status,
  isEmpty,
  isUploadingAvatar = false,
  isUploadingCover = false,
  mediaUploadErrorMessage,
  onUploadAvatar,
  onUploadCover,
  onSendMessage,
}: AiAccountDetailsProps) {
  if (status === 'idle' || status === 'loading') return <LoadingState variant="detail" />

  if (status === 'error') {
    return <EmptyState icon={CircleUserRound} title="好友资料暂不可用" description="请在好友列表中重新加载数据。" />
  }

  if (isEmpty) {
    return <EmptyState icon={CircleUserRound} title="还没有好友" description="从“寻找新朋友”开始，认识第一位可以长期相处的伙伴。" />
  }

  if (!account) {
    return <EmptyState icon={CircleUserRound} title="选择一位好友" description="从左侧好友列表选择一位朋友，查看对方的完整资料。" />
  }

  const statusPresentation = onlineStatusPresentation[account.onlineStatus]

  return (
    <article className="min-h-full bg-surface-muted pb-10">
      <div className="friend-profile-cover">
        {account.coverUrl ? (
          <img
            className="friend-profile-cover-image"
            src={account.coverUrl}
            alt=""
            width={1600}
            height={480}
            loading="eager"
            fetchPriority="high"
          />
        ) : (
          <>
            <span className="friend-profile-orbit friend-profile-orbit-one" aria-hidden="true" />
            <span className="friend-profile-orbit friend-profile-orbit-two" aria-hidden="true" />
          </>
        )}
        {onUploadCover && (
          <ProfileMediaUploadButton
            inputId={`friend-cover-upload-${account.id}`}
            mediaKind="cover"
            isUploading={isUploadingCover}
            disabled={isUploadingAvatar}
            className="absolute top-4 right-5 z-10 border-white/35 bg-surface/92 text-foreground hover:bg-surface"
            onUpload={onUploadCover}
          />
        )}
      </div>

      <div className="mx-auto -mt-12 w-full max-w-5xl px-6 xl:px-10">
        <header className="relative border-b border-border bg-surface px-6 pt-6 pb-7">
          <div className="flex min-w-0 items-end gap-5">
            <span className="relative shrink-0">
              <EntityAvatar
                name={account.nickname}
                src={account.avatarUrl}
                loading="eager"
                size="large"
                className="size-24 rounded-2xl ring-4 ring-surface"
              />
              {onUploadAvatar && (
                <ProfileMediaUploadButton
                  inputId={`friend-avatar-upload-${account.id}`}
                  mediaKind="avatar"
                  isUploading={isUploadingAvatar}
                  disabled={isUploadingCover}
                  compact
                  className="absolute -top-2 -right-2 z-10 size-8 rounded-full bg-surface shadow-message"
                  onUpload={onUploadAvatar}
                />
              )}
              <span
                className={`absolute right-0 bottom-1 size-4 rounded-full border-[3px] border-surface ${statusPresentation.className}`}
                aria-hidden="true"
              />
            </span>
            <div className="min-w-0 flex-1 pb-1">
              <div className="mb-1 flex min-w-0 flex-wrap items-center gap-2">
                <h1 className="truncate font-display text-[28px] leading-tight font-semibold tracking-[-0.02em]">
                  {account.nickname}
                </h1>
                <span className="inline-flex items-center gap-1.5 rounded-full bg-primary-soft px-2.5 py-1 text-xs font-semibold text-primary">
                  <span className={`size-1.5 rounded-full ${statusPresentation.className}`} aria-hidden="true" />
                  {statusPresentation.label}
                </span>
              </div>
              <p className="text-sm font-medium text-foreground">VC号：{account.vcNumber}</p>
              <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">
                {account.identityDescription || '暂未填写个人介绍'}
              </p>
            </div>
            {onSendMessage && (
              <Button className="mb-1 shrink-0" onClick={onSendMessage}>
                <MessageCircleMore className="size-4" aria-hidden="true" />
                发送消息
              </Button>
            )}
          </div>
        </header>

        {mediaUploadErrorMessage && (
          <div
            className="mt-4 rounded-lg border border-destructive/20 bg-danger-soft px-4 py-3 text-sm text-destructive"
            role="alert"
          >
            <strong className="font-semibold">图片上传失败：</strong>
            {mediaUploadErrorMessage}
          </div>
        )}

        <section className="mt-5 overflow-hidden rounded-xl border border-border bg-surface" aria-labelledby="friend-basic-profile-title">
          <h2 id="friend-basic-profile-title" className="sr-only">基础资料</h2>
          <dl className="grid [grid-template-columns:repeat(auto-fit,minmax(104px,1fr))]">
            <ProfileFact icon={CakeSlice} label="生日" value={formatBirthday(account.birthday)} />
            <ProfileFact icon={Star} label="星座" value={account.zodiacSign ?? '未设置'} />
            <ProfileFact icon={CalendarClock} label="年龄" value={account.age === null ? '未设置' : `${account.age}`} />
            <ProfileFact icon={VenusAndMars} label="性别" value={genderLabels[account.gender]} />
            <ProfileFact icon={MapPin} label="所在地" value={account.location || '未设置'} />
            <ProfileFact icon={BriefcaseBusiness} label="职业" value={account.occupation || '未设置'} />
            <ProfileFact icon={House} label="故乡" value={account.hometown || '未设置'} />
          </dl>
        </section>

        <section className="mt-4 grid gap-4 lg:grid-cols-[1fr_1fr_1.2fr]" aria-label="好友个性资料">
          <TagSection title="兴趣爱好" values={account.interestTags} emptyText="暂未添加兴趣标签" tone="accent" />
          <TagSection title="个性标签" values={account.personalityTags} emptyText="暂未添加个性标签" tone="primary" />
          <section className="rounded-xl border border-border bg-surface px-5 py-4">
            <div className="flex items-center gap-2">
              <Quote className="size-4 text-primary" aria-hidden="true" />
              <h2 className="text-sm font-semibold text-foreground">个性签名</h2>
            </div>
            <blockquote className="mt-4 whitespace-pre-wrap text-sm leading-7 text-foreground">
              {account.signature || '暂未填写个性签名。'}
            </blockquote>
          </section>
        </section>

        <section className="mt-4 rounded-xl border border-border bg-surface px-6 py-5">
          <div className="flex items-center gap-2">
            <MessageCircleMore className="size-4 text-primary" aria-hidden="true" />
            <h2 className="text-sm font-semibold text-foreground">相处资料</h2>
          </div>
          <dl className="mt-4 grid gap-x-8 gap-y-5 lg:grid-cols-2">
            <ProfileDescription label="性格描述" value={account.personality || '暂未填写'} />
            <ProfileDescription label="说话风格" value={account.speakingStyle || '暂未填写'} />
          </dl>
        </section>
      </div>
    </article>
  )
}

function ProfileFact({ icon: Icon, label, value }: { icon: typeof CakeSlice; label: string; value: string }) {
  return (
    <div className="min-w-0 border-r border-b border-border px-3 py-5 text-center last:border-r-0">
      <Icon className="mx-auto size-5 text-primary" strokeWidth={1.8} aria-hidden="true" />
      <dt className="mt-2 text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-1 truncate text-sm font-semibold text-foreground" title={value}>{value}</dd>
    </div>
  )
}

function TagSection({ title, values, emptyText, tone }: { title: string; values: string[]; emptyText: string; tone: 'primary' | 'accent' }) {
  const toneClassName = tone === 'primary' ? 'bg-primary-soft text-primary' : 'bg-surface-muted text-foreground'
  return (
    <section className="rounded-xl border border-border bg-surface px-5 py-4">
      <div className="flex items-center gap-2">
        <Sparkles className="size-4 text-primary" aria-hidden="true" />
        <h2 className="text-sm font-semibold text-foreground">{title}</h2>
      </div>
      {values.length > 0 ? (
        <ul className="mt-4 flex flex-wrap gap-2" aria-label={title}>
          {values.map((value) => (
            <li key={value} className={`max-w-full truncate rounded-full px-3 py-1.5 text-xs font-medium ${toneClassName}`} title={value}>
              {value}
            </li>
          ))}
        </ul>
      ) : (
        <p className="mt-4 text-sm text-muted-foreground">{emptyText}</p>
      )}
    </section>
  )
}

function ProfileDescription({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0 border-t border-border pt-4">
      <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
      <dd className="mt-2 whitespace-pre-wrap text-sm leading-6 text-foreground">{value}</dd>
    </div>
  )
}

function formatBirthday(value: string | null): string {
  if (!value) return '未设置'
  const parts = value.split('-')
  return parts.length === 3 ? `${Number(parts[1])}月${Number(parts[2])}日` : value
}
