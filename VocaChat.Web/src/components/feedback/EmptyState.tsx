import type { LucideIcon } from 'lucide-react'

interface EmptyStateProps {
  icon: LucideIcon
  title: string
  description: string
  compact?: boolean
}

export function EmptyState({
  icon: Icon,
  title,
  description,
  compact = false,
}: EmptyStateProps) {
  return (
    <div
      role="status"
      className={
        compact
          ? 'grid justify-items-start gap-2 px-6 py-8 text-left'
          : 'grid h-full min-h-72 place-content-center justify-items-center gap-3 px-8 text-center'
      }
    >
      <span className="grid size-11 place-items-center rounded-lg border border-border bg-surface-muted text-muted-foreground">
        <Icon className="size-5" strokeWidth={1.75} aria-hidden="true" />
      </span>
      <div className={compact ? 'grid gap-1' : 'grid max-w-sm gap-1'}>
        <h2 className="font-display text-base font-semibold text-foreground">
          {title}
        </h2>
        <p className="text-sm leading-6 text-muted-foreground">{description}</p>
      </div>
    </div>
  )
}
