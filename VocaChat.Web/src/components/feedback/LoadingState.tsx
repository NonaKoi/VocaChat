import { Skeleton } from '@/components/ui/skeleton'

interface LoadingStateProps {
  variant: 'list' | 'detail'
}

export function LoadingState({ variant }: LoadingStateProps) {
  if (variant === 'detail') {
    return (
      <div className="mx-auto w-full max-w-3xl px-10 py-12" role="status">
        <span className="sr-only">正在加载详情…</span>
        <div className="flex items-center gap-5 border-b border-border pb-8">
          <Skeleton className="size-20 rounded-xl" />
          <div className="grid flex-1 gap-3">
            <Skeleton className="h-3 w-20" />
            <Skeleton className="h-7 w-48" />
            <Skeleton className="h-4 w-64 max-w-full" />
          </div>
        </div>
        <div className="mt-8 grid gap-6">
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-4/5" />
        </div>
      </div>
    )
  }

  return (
    <div className="grid gap-1.5 px-2 py-3" role="status">
      <span className="sr-only">正在加载列表…</span>
      {Array.from({ length: 5 }, (_, index) => (
        <div key={index} className="flex items-center gap-3 px-3 py-2.5">
          <Skeleton className="size-11 shrink-0 rounded-lg" />
          <div className="grid flex-1 gap-2">
            <Skeleton className="h-3.5 w-2/5" />
            <Skeleton className="h-3 w-4/5" />
          </div>
        </div>
      ))}
    </div>
  )
}
