import { useId } from 'react'
import type { AutonomyLevel } from '@/api/types'
import { cn } from '@/lib/utils'

const levelOptions: Array<{ value: AutonomyLevel; label: string }> = [
  { value: 'Low', label: '低' },
  { value: 'Normal', label: '适中' },
  { value: 'High', label: '高' },
]

interface SettingsLevelSelectorProps {
  label: string
  description: string
  value: AutonomyLevel
  disabled?: boolean
  onValueChange: (value: AutonomyLevel) => void
}

/** 在设置页中统一呈现低、适中、高三个语义档位。 */
export function SettingsLevelSelector({
  label,
  description,
  value,
  disabled = false,
  onValueChange,
}: SettingsLevelSelectorProps) {
  const labelId = useId()

  return (
    <div className="flex flex-wrap items-center justify-between gap-4 px-5 py-4">
      <div>
        <span id={labelId} className="text-sm font-medium text-foreground">
          {label}
        </span>
        <p className="mt-1 text-xs leading-5 text-muted-foreground">
          {description}
        </p>
      </div>
      <div
        className="grid min-w-64 grid-cols-3 rounded-lg border border-border bg-surface-muted p-1"
        role="radiogroup"
        aria-labelledby={labelId}
      >
        {levelOptions.map((option) => (
          <button
            key={option.value}
            type="button"
            role="radio"
            aria-checked={value === option.value}
            disabled={disabled}
            onClick={() => onValueChange(option.value)}
            className={cn(
              'rounded-md px-3 py-1.5 text-sm font-medium text-muted-foreground outline-none transition-colors hover:bg-surface hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-45',
              value === option.value
                && 'bg-surface text-primary ring-1 ring-border',
            )}
          >
            {option.label}
          </button>
        ))}
      </div>
    </div>
  )
}
