import { useState } from 'react'
import { Eye, EyeOff, KeyRound, RotateCcw, Trash2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import type { AiModelConnectionDraft } from '@/components/settings/aiModelConnectionForm'

interface AiModelConnectionFieldsProps {
  idPrefix: string
  draft: AiModelConnectionDraft
  hasApiKey: boolean
  disabled?: boolean
  onChange: (values: Partial<AiModelConnectionDraft>) => void
}

/**
 * 编辑一套完整的 OpenAI 兼容接口连接；密钥输入框永不回填已保存原文。
 */
export function AiModelConnectionFields({
  idPrefix,
  draft,
  hasApiKey,
  disabled = false,
  onChange,
}: AiModelConnectionFieldsProps) {
  const [showsApiKey, setShowsApiKey] = useState(false)
  const apiKeyStatus = draft.clearApiKey
    ? '保存后将清除密钥'
    : hasApiKey
      ? '已安全保存密钥'
      : '当前未设置密钥'

  return (
    <>
      <div className="bg-surface-muted px-5 py-3">
        <div className="flex items-center gap-2 text-sm font-semibold text-foreground">
          <KeyRound className="size-4 text-primary" strokeWidth={1.8} aria-hidden="true" />
          AI 接口
        </div>
        <p className="mt-1 max-w-2xl text-xs leading-5 text-muted-foreground">
          填写 OpenAI 兼容接口地址与模型名称。API Key 留空时不会改变已经保存的密钥。
        </p>
      </div>

      <SettingsTextField
        id={`${idPrefix}-base-url`}
        label="API 地址"
        description="例如 https://api.example.com/v1/；请求会发送到该地址下的 chat/completions。"
        value={draft.baseUrl}
        disabled={disabled}
        inputMode="url"
        placeholder="https://api.example.com/v1/"
        onValueChange={(baseUrl) => onChange({ baseUrl })}
      />

      <SettingsTextField
        id={`${idPrefix}-model`}
        label="模型名称"
        description="使用接口服务商提供的准确模型标识。"
        value={draft.model}
        disabled={disabled}
        placeholder="model-name"
        onValueChange={(model) => onChange({ model })}
      />

      <div className="flex flex-wrap items-start justify-between gap-4 px-5 py-4">
        <div className="min-w-0 flex-1">
          <label
            htmlFor={`${idPrefix}-api-key`}
            className="text-sm font-medium text-foreground"
          >
            API Key
          </label>
          <p
            id={`${idPrefix}-api-key-description`}
            className="mt-1 max-w-2xl text-xs leading-5 text-muted-foreground"
          >
            页面不会读取或显示已经保存的密钥。填写新值可替换，留空则保持不变。
          </p>
          <p
            className={cn(
              'mt-1 text-xs',
              draft.clearApiKey ? 'text-destructive' : 'text-muted-foreground',
            )}
            aria-live="polite"
          >
            {apiKeyStatus}
          </p>
        </div>

        <div className="w-full shrink-0 sm:w-[min(100%,360px)]">
          <div className="flex h-9 items-center rounded-lg border border-border bg-surface focus-within:border-primary/50 focus-within:ring-2 focus-within:ring-ring/30">
            <input
              id={`${idPrefix}-api-key`}
              name={`${idPrefix}-api-key`}
              type={showsApiKey ? 'text' : 'password'}
              autoComplete="new-password"
              autoCapitalize="none"
              spellCheck={false}
              value={draft.apiKey}
              disabled={disabled}
              aria-describedby={`${idPrefix}-api-key-description`}
              placeholder={hasApiKey ? '输入新密钥以替换' : '可选'}
              onChange={(event) => onChange({
                apiKey: event.target.value,
                clearApiKey: false,
              })}
              className="min-w-0 flex-1 bg-transparent px-3 text-sm text-foreground outline-none placeholder:text-muted-foreground disabled:cursor-not-allowed disabled:text-muted-foreground"
            />
            <button
              type="button"
              aria-label={showsApiKey ? '隐藏 API Key' : '显示 API Key'}
              disabled={disabled}
              onClick={() => setShowsApiKey((current) => !current)}
              className="grid size-9 shrink-0 place-items-center rounded-md text-muted-foreground outline-none hover:bg-primary-soft hover:text-primary focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-45"
            >
              {showsApiKey ? (
                <EyeOff className="size-4" strokeWidth={1.8} aria-hidden="true" />
              ) : (
                <Eye className="size-4" strokeWidth={1.8} aria-hidden="true" />
              )}
            </button>
          </div>

          {(hasApiKey || draft.clearApiKey) && (
            <Button
              variant="ghost"
              className={cn(
                'mt-2 h-8 px-2 text-xs',
                !draft.clearApiKey && 'hover:bg-destructive/8 hover:text-destructive',
              )}
              disabled={disabled}
              onClick={() => onChange({
                apiKey: '',
                clearApiKey: !draft.clearApiKey,
              })}
            >
              {draft.clearApiKey ? (
                <RotateCcw className="size-3.5" strokeWidth={1.8} aria-hidden="true" />
              ) : (
                <Trash2 className="size-3.5" strokeWidth={1.8} aria-hidden="true" />
              )}
              {draft.clearApiKey ? '撤销清除' : '清除已保存的密钥'}
            </Button>
          )}
        </div>
      </div>
    </>
  )
}

function SettingsTextField({
  id,
  label,
  description,
  value,
  disabled,
  placeholder,
  inputMode,
  onValueChange,
}: {
  id: string
  label: string
  description: string
  value: string
  disabled: boolean
  placeholder: string
  inputMode?: 'text' | 'url'
  onValueChange: (value: string) => void
}) {
  const descriptionId = `${id}-description`

  return (
    <div className="flex flex-wrap items-center justify-between gap-4 px-5 py-4">
      <div className="min-w-0 flex-1">
        <label htmlFor={id} className="text-sm font-medium text-foreground">
          {label}
        </label>
        <p id={descriptionId} className="mt-1 max-w-2xl text-xs leading-5 text-muted-foreground">
          {description}
        </p>
      </div>
      <input
        id={id}
        name={id}
        type="text"
        inputMode={inputMode}
        autoComplete="off"
        autoCapitalize="none"
        spellCheck={false}
        value={value}
        disabled={disabled}
        aria-describedby={descriptionId}
        placeholder={placeholder}
        onChange={(event) => onValueChange(event.target.value)}
        className="h-9 w-full rounded-lg border border-border bg-surface px-3 text-sm text-foreground outline-none placeholder:text-muted-foreground focus:border-primary/50 focus:ring-2 focus:ring-ring/30 disabled:cursor-not-allowed disabled:bg-surface-muted disabled:text-muted-foreground sm:w-[min(100%,360px)]"
      />
    </div>
  )
}
