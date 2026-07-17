import { RotateCcw, WifiOff } from 'lucide-react'
import { Button } from '@/components/ui/button'

interface ErrorStateProps {
  message?: string
  onRetry: () => void
}

export function ErrorState({ message, onRetry }: ErrorStateProps) {
  return (
    <div className="m-4 grid justify-items-start gap-3 rounded-lg border border-destructive/20 bg-danger-soft p-4 text-left">
      <span className="grid size-9 place-items-center rounded-md bg-surface text-destructive ring-1 ring-destructive/10">
        <WifiOff className="size-[18px]" strokeWidth={1.75} aria-hidden="true" />
      </span>
      <div className="grid gap-1">
        <strong className="text-sm font-semibold text-foreground">
          无法连接到 VocaChat Web API
        </strong>
        <p className="break-words text-xs leading-5 text-muted-foreground">
          {message ?? '请求没有完成，请确认后端已经启动。'}
        </p>
      </div>
      <Button variant="outline" onClick={onRetry}>
        <RotateCcw className="size-4" strokeWidth={1.75} aria-hidden="true" />
        重新加载
      </Button>
    </div>
  )
}
