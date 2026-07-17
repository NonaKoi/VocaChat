import {
  useEffect,
  useRef,
  type FormEvent,
  type KeyboardEvent,
} from 'react'
import { LoaderCircle, Paperclip, SendHorizontal, Smile } from 'lucide-react'
import { Button } from '@/components/ui/button'
import type { MessageSendOutcome } from '@/hooks/useGroupMessages'

const MESSAGE_MAX_LENGTH = 4000
const TEXTAREA_MAX_HEIGHT = 144

interface MessageComposerProps {
  value: string
  disabled: boolean
  isSending: boolean
  disabledReason?: string
  onValueChange: (value: string) => void
  onSend: (content: string) => Promise<MessageSendOutcome>
}

/** 负责可增长的消息输入和键盘提交，不直接访问后端。 */
export function MessageComposer({
  value,
  disabled,
  isSending,
  disabledReason,
  onValueChange,
  onSend,
}: MessageComposerProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const canSubmit = !disabled && !isSending && value.trim().length > 0
  const shouldShowCount = value.length >= MESSAGE_MAX_LENGTH * 0.8

  useEffect(() => {
    const textarea = textareaRef.current
    if (!textarea) {
      return
    }

    textarea.style.height = 'auto'
    textarea.style.height = `${Math.min(textarea.scrollHeight, TEXTAREA_MAX_HEIGHT)}px`
    textarea.style.overflowY =
      textarea.scrollHeight > TEXTAREA_MAX_HEIGHT ? 'auto' : 'hidden'
  }, [value])

  async function submitMessage(event?: FormEvent): Promise<void> {
    event?.preventDefault()

    if (!canSubmit) {
      return
    }

    const outcome = await onSend(value)

    // 部分失败时用户消息已经保存，清空草稿可避免重试造成重复消息。
    if (outcome === 'success' || outcome === 'partial') {
      onValueChange('')
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>): void {
    if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault()
      void submitMessage()
    }
  }

  return (
    <form
      className="border-t border-border bg-surface px-5 py-4"
      onSubmit={(event) => void submitMessage(event)}
    >
      <div className="mx-auto flex w-full max-w-4xl items-end gap-2 rounded-xl border border-border bg-surface px-2 py-2 transition-[border-color,box-shadow] focus-within:border-primary/40 focus-within:ring-2 focus-within:ring-primary/10">
        <Button
          type="button"
          variant="ghost"
          size="icon"
          disabled
          aria-label="添加附件，后续阶段开放"
          title="附件功能将在后续阶段开放"
          className="size-9"
        >
          <Paperclip className="size-[18px]" aria-hidden="true" />
        </Button>
        <textarea
          ref={textareaRef}
          rows={1}
          name="message-content"
          autoComplete="off"
          maxLength={MESSAGE_MAX_LENGTH}
          value={value}
          disabled={disabled || isSending}
          onChange={(event) => onValueChange(event.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={disabledReason ?? '输入消息…'}
          aria-label="消息内容"
          className="min-h-9 min-w-0 flex-1 resize-none bg-transparent px-1 py-2 text-sm leading-5 text-foreground outline-none placeholder:text-muted-foreground disabled:cursor-not-allowed"
        />
        <Button
          type="button"
          variant="ghost"
          size="icon"
          disabled
          aria-label="选择表情，后续阶段开放"
          title="表情功能将在后续阶段开放"
          className="size-9"
        >
          <Smile className="size-[18px]" aria-hidden="true" />
        </Button>
        <Button
          type="submit"
          size="icon"
          disabled={!canSubmit}
          aria-label={isSending ? '正在发送消息…' : '发送消息'}
          className="size-9 rounded-lg"
        >
          {isSending ? (
            <LoaderCircle className="size-[17px] animate-spin" aria-hidden="true" />
          ) : (
            <SendHorizontal className="size-[17px]" aria-hidden="true" />
          )}
        </Button>
      </div>
      <div className="mx-auto mt-2 flex w-full max-w-4xl justify-between gap-3 px-1 text-[11px] text-muted-foreground">
        <p>Enter 发送 · Shift + Enter 换行</p>
        {shouldShowCount && (
          <p className="tabular-nums" aria-live="polite">
            {value.length} / {MESSAGE_MAX_LENGTH}
          </p>
        )}
      </div>
    </form>
  )
}
