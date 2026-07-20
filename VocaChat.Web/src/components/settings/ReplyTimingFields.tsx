import { Clock3 } from 'lucide-react'
import type { AiReplyDelayMode } from '@/api/types'
import {
  millisecondsToSeconds,
  secondsToMilliseconds,
} from '@/components/settings/replyTiming'
import { cn } from '@/lib/utils'

interface ReplyTimingFieldsProps {
  idPrefix: string
  title?: string
  description?: string
  mode: AiReplyDelayMode
  fixedDelayMilliseconds: number
  minimumDelayMilliseconds: number
  maximumDelayMilliseconds: number
  disabled?: boolean
  onModeChange: (mode: AiReplyDelayMode) => void
  onFixedDelayChange: (value: number) => void
  onMinimumDelayChange: (value: number) => void
  onMaximumDelayChange: (value: number) => void
}

/** 在设置页中统一编辑固定或随机范围回复间隔。界面使用秒，API 使用毫秒。 */
export function ReplyTimingFields({
  idPrefix,
  title = '回复速度',
  description = '控制好友消息之间的等待时间；模型生成已花费的时间会计入间隔。',
  mode,
  fixedDelayMilliseconds,
  minimumDelayMilliseconds,
  maximumDelayMilliseconds,
  disabled = false,
  onModeChange,
  onFixedDelayChange,
  onMinimumDelayChange,
  onMaximumDelayChange,
}: ReplyTimingFieldsProps) {
  const rangeIsInvalid = minimumDelayMilliseconds > maximumDelayMilliseconds

  return (
    <fieldset className="px-5 py-4" disabled={disabled}>
      <legend className="sr-only">{title}</legend>
      <div className="flex items-start gap-3">
        <span className="mt-0.5 grid size-9 shrink-0 place-items-center rounded-lg bg-primary-soft text-primary">
          <Clock3 className="size-[18px]" strokeWidth={1.75} aria-hidden="true" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium text-foreground">{title}</p>
          <p className="mt-1 text-xs leading-5 text-muted-foreground">
            {description}
          </p>

          <div className="mt-3 grid gap-2 sm:grid-cols-2" role="radiogroup" aria-label={`${title}模式`}>
            <TimingModeOption
              id={`${idPrefix}-fixed`}
              label="固定间隔"
              description="每条消息使用相同等待时间"
              checked={mode === 'Fixed'}
              onChange={() => onModeChange('Fixed')}
            />
            <TimingModeOption
              id={`${idPrefix}-random`}
              label="随机区间"
              description="每条消息在自定范围内变化"
              checked={mode === 'RandomRange'}
              onChange={() => onModeChange('RandomRange')}
            />
          </div>

          {mode === 'Fixed' ? (
            <DelayInput
              id={`${idPrefix}-fixed-seconds`}
              label="固定等待"
              groupTitle={title}
              value={fixedDelayMilliseconds}
              onChange={onFixedDelayChange}
            />
          ) : (
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <DelayInput
                id={`${idPrefix}-minimum-seconds`}
                label="最短等待"
                groupTitle={title}
                value={minimumDelayMilliseconds}
                invalid={rangeIsInvalid}
                onChange={onMinimumDelayChange}
              />
              <DelayInput
                id={`${idPrefix}-maximum-seconds`}
                label="最长等待"
                groupTitle={title}
                value={maximumDelayMilliseconds}
                invalid={rangeIsInvalid}
                onChange={onMaximumDelayChange}
              />
            </div>
          )}

          {rangeIsInvalid && mode === 'RandomRange' && (
            <p className="mt-2 text-xs text-destructive" role="alert">
              最短等待不能大于最长等待。
            </p>
          )}
          <p className="mt-2 text-[11px] leading-5 text-muted-foreground">
            单位为秒，不设置产品上限；数值必须为非负数。
          </p>
        </div>
      </div>
    </fieldset>
  )
}

function TimingModeOption({
  id,
  label,
  description,
  checked,
  onChange,
}: {
  id: string
  label: string
  description: string
  checked: boolean
  onChange: () => void
}) {
  return (
    <label
      htmlFor={id}
      className={cn(
        'flex cursor-pointer gap-2.5 rounded-lg border border-border px-3 py-2.5 transition-colors focus-within:ring-2 focus-within:ring-ring',
        checked && 'border-primary/40 bg-primary-soft',
      )}
    >
      <input
        id={id}
        type="radio"
        name={`${id.slice(0, id.lastIndexOf('-'))}-mode`}
        checked={checked}
        onChange={onChange}
        className="mt-0.5 accent-[hsl(var(--primary))]"
      />
      <span className="min-w-0">
        <span className="block text-xs font-medium text-foreground">{label}</span>
        <span className="mt-0.5 block text-[11px] leading-4 text-muted-foreground">
          {description}
        </span>
      </span>
    </label>
  )
}

function DelayInput({
  id,
  label,
  groupTitle,
  value,
  invalid = false,
  onChange,
}: {
  id: string
  label: string
  groupTitle: string
  value: number
  invalid?: boolean
  onChange: (value: number) => void
}) {
  return (
    <label htmlFor={id} className="mt-3 block text-xs text-muted-foreground sm:mt-0">
      {label}
      <span className={cn(
        'mt-1.5 flex h-9 items-center overflow-hidden rounded-lg border border-border bg-surface focus-within:border-primary/50 focus-within:ring-2 focus-within:ring-ring/30',
        invalid && 'border-destructive/60',
      )}>
        <input
          id={id}
          name={id}
          type="number"
          min={0}
          step={0.1}
          inputMode="decimal"
          autoComplete="off"
          value={millisecondsToSeconds(value)}
          aria-label={`${groupTitle}：${label}`}
          aria-invalid={invalid}
          onChange={(event) => onChange(secondsToMilliseconds(event.target.value))}
          className="min-w-0 flex-1 bg-transparent px-3 text-right text-sm tabular-nums text-foreground outline-none disabled:cursor-not-allowed disabled:text-muted-foreground"
        />
        <span className="border-l border-border px-2 text-xs text-muted-foreground" aria-hidden="true">
          秒
        </span>
      </span>
    </label>
  )
}
