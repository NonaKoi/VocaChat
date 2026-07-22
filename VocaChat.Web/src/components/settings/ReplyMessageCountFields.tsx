import { MessagesSquare } from 'lucide-react'
import { isReplyMessageCountRangeValid } from '@/components/settings/replyMessageCount'
import { cn } from '@/lib/utils'

interface ReplyMessageCountFieldsProps {
  idPrefix: string
  minimum: number
  maximum: number
  disabled?: boolean
  onMinimumChange: (value: number) => void
  onMaximumChange: (value: number) => void
}

/** 设置一次回复可以自然拆成的独立消息条数范围。 */
export function ReplyMessageCountFields({
  idPrefix,
  minimum,
  maximum,
  disabled = false,
  onMinimumChange,
  onMaximumChange,
}: ReplyMessageCountFieldsProps) {
  const isInvalid = !isReplyMessageCountRangeValid(minimum, maximum)
  const errorId = `${idPrefix}-error`

  return (
    <fieldset className="px-5 py-4" disabled={disabled}>
      <legend className="sr-only">单次回复消息条数</legend>
      <div className="flex items-start gap-3">
        <span className="mt-0.5 grid size-9 shrink-0 place-items-center rounded-lg bg-primary-soft text-primary">
          <MessagesSquare className="size-[18px]" strokeWidth={1.75} aria-hidden="true" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium text-foreground">单次回复消息条数</p>
          <p className="mt-1 text-xs leading-5 text-muted-foreground">
            导演会根据内容完整性在范围内选择条数；一条消息足以说清时不会为了凑数拆分。
          </p>
          <div className="mt-3 grid gap-3 sm:grid-cols-2">
            <CountInput
              id={`${idPrefix}-minimum`}
              label="最少条数"
              value={minimum}
              invalid={isInvalid}
              errorId={errorId}
              onChange={onMinimumChange}
            />
            <CountInput
              id={`${idPrefix}-maximum`}
              label="最多条数"
              value={maximum}
              invalid={isInvalid}
              errorId={errorId}
              onChange={onMaximumChange}
            />
          </div>
          {isInvalid && (
            <p id={errorId} className="mt-2 text-xs text-destructive" role="alert">
              请输入 1 到 4 之间的整数，且最少条数不能大于最多条数。
            </p>
          )}
        </div>
      </div>
    </fieldset>
  )
}

function CountInput({
  id,
  label,
  value,
  invalid,
  errorId,
  onChange,
}: {
  id: string
  label: string
  value: number
  invalid: boolean
  errorId: string
  onChange: (value: number) => void
}) {
  return (
    <label htmlFor={id} className="text-xs text-muted-foreground">
      {label}
      <span className={cn(
        'mt-1.5 flex h-9 items-center overflow-hidden rounded-lg border border-border bg-surface focus-within:border-primary/50 focus-within:ring-2 focus-within:ring-ring/30',
        invalid && 'border-destructive/60',
      )}>
        <input
          id={id}
          name={id}
          type="number"
          min={1}
          max={4}
          step={1}
          inputMode="numeric"
          autoComplete="off"
          value={value}
          aria-label={label}
          aria-invalid={invalid}
          aria-describedby={invalid ? errorId : undefined}
          onChange={(event) => onChange(Number(event.target.value))}
          className="min-w-0 flex-1 bg-transparent px-3 text-right text-sm tabular-nums text-foreground outline-none disabled:cursor-not-allowed disabled:text-muted-foreground"
        />
        <span className="border-l border-border px-2 text-xs text-muted-foreground" aria-hidden="true">
          条
        </span>
      </span>
    </label>
  )
}
