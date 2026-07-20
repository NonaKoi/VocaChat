import { useId, useState, type KeyboardEvent } from 'react'
import { Plus, X } from 'lucide-react'
import { Button } from '@/components/ui/button'

interface ProfileTagInputProps {
  id: string
  label: string
  description: string
  values: string[]
  onChange: (values: string[]) => void
}

const maximumTagCount = 12
const maximumTagLength = 30

/** 以独立字符串数组编辑档案标签，避免把标签压缩成逗号字符串。 */
export function ProfileTagInput({
  id,
  label,
  description,
  values,
  onChange,
}: ProfileTagInputProps) {
  const [draft, setDraft] = useState('')
  const [errorMessage, setErrorMessage] = useState<string>()
  const errorId = useId()

  function addTag(): void {
    const normalizedValue = draft.trim()

    if (!normalizedValue) {
      return
    }

    if (normalizedValue.length > maximumTagLength) {
      setErrorMessage(`单个标签不能超过 ${maximumTagLength} 个字符。`)
      return
    }

    if (values.length >= maximumTagCount) {
      setErrorMessage(`最多添加 ${maximumTagCount} 个标签。`)
      return
    }

    if (
      values.some(
        (value) => value.toLocaleLowerCase() === normalizedValue.toLocaleLowerCase(),
      )
    ) {
      setErrorMessage('这个标签已经添加。')
      return
    }

    onChange([...values, normalizedValue])
    setDraft('')
    setErrorMessage(undefined)
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>): void {
    if (event.key === 'Enter' || event.key === ',' || event.key === '，') {
      event.preventDefault()
      addTag()
    }
  }

  function removeTag(tagToRemove: string): void {
    onChange(values.filter((value) => value !== tagToRemove))
    setErrorMessage(undefined)
  }

  return (
    <fieldset className="grid gap-2">
      <legend className="text-sm font-semibold text-foreground">{label}</legend>
      <p id={`${id}-description`} className="text-xs text-muted-foreground">
        {description}
      </p>

      {values.length > 0 && (
        <ul className="flex flex-wrap gap-2" aria-label={`已添加的${label}`}>
          {values.map((value) => (
            <li
              key={value}
              className="inline-flex min-h-8 items-center gap-1 rounded-full bg-primary-soft py-1 pr-1 pl-3 text-xs font-medium text-primary"
            >
              <span className="max-w-48 truncate">{value}</span>
              <button
                type="button"
                className="grid size-6 place-items-center rounded-full transition-colors hover:bg-primary/10 focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-focus"
                onClick={() => removeTag(value)}
                aria-label={`删除标签 ${value}`}
              >
                <X className="size-3.5" aria-hidden="true" />
              </button>
            </li>
          ))}
        </ul>
      )}

      <div className="flex gap-2">
        <input
          id={id}
          name={id}
          type="text"
          aria-label={`添加${label}`}
          autoComplete="off"
          spellCheck={false}
          maxLength={maximumTagLength + 1}
          value={draft}
          onChange={(event) => {
            setDraft(event.target.value)
            setErrorMessage(undefined)
          }}
          onKeyDown={handleKeyDown}
          aria-describedby={`${id}-description${errorMessage ? ` ${errorId}` : ''}`}
          aria-invalid={Boolean(errorMessage)}
          placeholder="输入后按 Enter 添加…"
          className="form-control min-w-0 flex-1"
        />
        <Button
          type="button"
          variant="outline"
          onClick={addTag}
          disabled={!draft.trim() || values.length >= maximumTagCount}
        >
          <Plus className="size-4" aria-hidden="true" />
          添加
        </Button>
      </div>

      <p
        id={errorId}
        className="min-h-4 text-xs text-destructive"
        aria-live="polite"
      >
        {errorMessage}
      </p>
    </fieldset>
  )
}
