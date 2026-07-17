import type { LucideIcon } from 'lucide-react'
import type { AppSection } from '@/types/appSection'
import { cn } from '@/lib/utils'

interface NavigationItemProps {
  section: AppSection
  label: string
  icon: LucideIcon
  active: boolean
  disabled?: boolean
  onSelect?: (section: AppSection) => void
}

export function NavigationItem({
  section,
  label,
  icon: Icon,
  active,
  disabled = false,
  onSelect,
}: NavigationItemProps) {
  return (
    <button
      type="button"
      className={cn(
        'group flex w-full items-center justify-center gap-3 rounded-lg px-3 py-3 text-sm font-medium text-navigation-muted outline-none transition-colors duration-200 xl:justify-start',
        'hover:bg-white/8 hover:text-white focus-visible:ring-2 focus-visible:ring-white/75 focus-visible:ring-offset-2 focus-visible:ring-offset-navigation',
        active && 'bg-white/12 text-white',
        disabled && 'cursor-not-allowed opacity-55 hover:bg-transparent hover:text-navigation-muted',
      )}
      aria-current={active ? 'page' : undefined}
      disabled={disabled}
      title={disabled ? `${label}将在后续阶段开放` : label}
      onClick={() => onSelect?.(section)}
    >
      <Icon className="size-[22px] shrink-0" strokeWidth={1.7} aria-hidden="true" />
      <span className="hidden xl:inline">{label}</span>
    </button>
  )
}
