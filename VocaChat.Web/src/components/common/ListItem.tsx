import type { ReactNode } from 'react'
import { ChevronRight } from 'lucide-react'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { cn } from '@/lib/utils'

interface ListItemProps {
  title: string
  description: string
  selected: boolean
  onSelect: () => void
  avatarLabel?: string
  avatarUrl?: string | null
  trailing?: ReactNode
}

export function ListItem({
  title,
  description,
  selected,
  onSelect,
  avatarLabel,
  avatarUrl,
  trailing,
}: ListItemProps) {
  return (
    <button
      type="button"
      className={cn(
        'group relative flex w-full min-w-0 items-center gap-3 overflow-hidden rounded-md border border-transparent px-3 py-2.5 text-left outline-none transition-colors',
        'before:absolute before:inset-y-2 before:left-0 before:w-0.5 before:rounded-full before:bg-primary before:opacity-0 before:transition-opacity',
        'hover:bg-surface-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1 focus-visible:ring-offset-surface',
        selected &&
          'border-primary/10 bg-primary-soft before:opacity-100 hover:bg-primary-soft',
      )}
      aria-current={selected ? 'true' : undefined}
      onClick={onSelect}
    >
      <EntityAvatar name={title} label={avatarLabel} src={avatarUrl} />
      <span className="grid min-w-0 flex-1 gap-1">
        <strong className="truncate text-sm font-semibold text-foreground">
          {title}
        </strong>
        <span className="truncate text-xs text-muted-foreground">
          {description}
        </span>
      </span>
      {trailing}
      <ChevronRight
        className={cn(
          'size-4 shrink-0 text-border transition-colors group-hover:text-muted-foreground',
          selected && 'text-primary/55',
        )}
        strokeWidth={1.75}
        aria-hidden="true"
      />
    </button>
  )
}
