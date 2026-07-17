import { cn } from '@/lib/utils'

interface SettingsToggleProps {
  id: string
  label: string
  description?: string
  checked: boolean
  disabled?: boolean
  onCheckedChange: (checked: boolean) => void
}

/** 使用原生按钮语义实现可键盘操作的设置开关。 */
export function SettingsToggle({
  id,
  label,
  description,
  checked,
  disabled = false,
  onCheckedChange,
}: SettingsToggleProps) {
  return (
    <div className="flex min-h-18 items-center justify-between gap-6 px-5 py-4">
      <div className="min-w-0">
        <span id={`${id}-label`} className="block text-sm font-medium text-foreground">
          {label}
        </span>
        {description && (
          <span className="mt-1 block text-xs leading-5 text-muted-foreground">
            {description}
          </span>
        )}
      </div>
      <button
        id={id}
        type="button"
        role="switch"
        aria-checked={checked}
        aria-labelledby={`${id}-label`}
        disabled={disabled}
        onClick={() => onCheckedChange(!checked)}
        className={cn(
          'relative h-6 w-11 shrink-0 rounded-full border outline-none transition-colors duration-200 focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-surface',
          checked ? 'border-primary bg-primary hover:bg-primary-hover' : 'border-border bg-surface-muted hover:border-primary/35',
          disabled && 'cursor-not-allowed opacity-45',
        )}
      >
        <span
          className={cn(
            'absolute top-0.5 left-0.5 size-[18px] rounded-full bg-white shadow-sm transition-transform duration-200',
            checked && 'translate-x-5',
          )}
          aria-hidden="true"
        />
      </button>
    </div>
  )
}
