import { Heart, Image, MessageCircle, RefreshCw } from 'lucide-react'
import { useState } from 'react'
import type { PostResponse } from '@/api/types'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { EmptyState } from '@/components/feedback/EmptyState'
import { ErrorState } from '@/components/feedback/ErrorState'
import { LoadingState } from '@/components/feedback/LoadingState'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import type { RemoteStatus } from '@/types/remoteStatus'
import { formatMessageTime } from '@/utils/dateTime'

interface ActivityFeedProps {
  posts: PostResponse[]
  status: RemoteStatus
  errorMessage?: string
  actionError?: string
  onRetry: () => void
  onToggleLike: (id: string, liked: boolean) => void
  onComment: (id: string, content: string) => void
}

export function ActivityFeed(props: ActivityFeedProps) {
  return <section className="h-full overflow-y-auto bg-surface-muted" aria-label="好友动态">
    <header className="sticky top-0 z-10 flex h-[72px] items-center justify-between border-b border-border bg-surface/95 px-7 backdrop-blur-sm">
      <div><h1 className="text-base font-semibold">好友动态</h1><p className="mt-0.5 text-xs text-muted-foreground">看看朋友们最近分享了什么</p></div>
      <Button variant="ghost" size="icon" onClick={props.onRetry} aria-label="刷新动态"><RefreshCw className="size-4" aria-hidden="true" /></Button>
    </header>
    <div className="mx-auto w-full max-w-3xl px-6 py-8">
      {props.actionError && <p role="alert" className="mb-4 rounded-lg border border-destructive/20 bg-danger-soft px-4 py-3 text-sm text-destructive">{props.actionError}</p>}
      {(props.status === 'idle' || props.status === 'loading') && <LoadingState variant="detail" />}
      {props.status === 'error' && <ErrorState message={props.errorMessage} onRetry={props.onRetry} />}
      {props.status === 'success' && props.posts.length === 0 && <EmptyState icon={Image} title="还没有动态" description="好友发布的生活片段会出现在这里。" />}
      <div className="grid gap-4">{props.posts.map((post) => <PostItem key={post.id} post={post} onToggleLike={props.onToggleLike} onComment={props.onComment} />)}</div>
    </div>
  </section>
}

function PostItem({ post, onToggleLike, onComment }: { post: PostResponse; onToggleLike: ActivityFeedProps['onToggleLike']; onComment: ActivityFeedProps['onComment'] }) {
  const [comment, setComment] = useState('')
  return <article className="overflow-hidden rounded-xl border border-border bg-surface">
    <div className="flex gap-3 px-5 pt-5"><EntityAvatar name={post.authorNickname} src={post.authorAvatarUrl} size="small" /><div className="min-w-0"><h2 className="truncate text-sm font-semibold">{post.authorNickname}</h2><time className="text-xs text-muted-foreground" dateTime={post.createdAt}>{formatMessageTime(post.createdAt)}</time></div></div>
    <p className="whitespace-pre-wrap px-5 pt-4 text-sm leading-7 text-foreground">{post.content}</p>
    {post.images.length > 0 && <div className={cn('mt-4 grid gap-1 px-5', post.images.length === 1 ? 'grid-cols-1' : 'grid-cols-2')}>
      {post.images.map((image) => <img key={image.id} src={image.imageUrl} alt="动态图片" width={1200} height={800} className="max-h-[420px] h-full w-full rounded-lg object-cover" loading="lazy" />)}
    </div>}
    <div className="mt-4 flex items-center gap-1 border-t border-border px-4 py-2">
      <Button variant="ghost" onClick={() => onToggleLike(post.id, post.isLikedByLocalUser)} className={cn('h-9 px-3', post.isLikedByLocalUser && 'text-destructive')}><Heart className={cn('size-4', post.isLikedByLocalUser && 'fill-current')} aria-hidden="true" />{post.likeCount}</Button>
      <span className="flex items-center gap-1 px-3 text-xs text-muted-foreground"><MessageCircle className="size-4" aria-hidden="true" />{post.commentCount}</span>
    </div>
    {(post.recentComments.length > 0 || post.commentCount >= 0) && <div className="border-t border-border bg-surface-muted px-5 py-3">
      {post.recentComments.map((item) => <p key={item.id} className="mb-1 text-xs leading-5"><strong>{item.senderDisplayName}：</strong>{item.content}</p>)}
      <form className="mt-2 flex gap-2" onSubmit={(event) => { event.preventDefault(); const value = comment.trim(); if (!value) return; onComment(post.id, value); setComment('') }}>
        <label className="sr-only" htmlFor={`comment-${post.id}`}>评论动态</label><input id={`comment-${post.id}`} name={`comment-${post.id}`} autoComplete="off" value={comment} onChange={(event) => setComment(event.target.value)} placeholder="写下评论…" className="h-9 min-w-0 flex-1 rounded-lg border border-border bg-surface px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring" /><Button type="submit" className="h-9 px-3" disabled={!comment.trim()}>发送</Button>
      </form>
    </div>}
  </article>
}
