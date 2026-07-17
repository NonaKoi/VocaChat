import { useRef, type ChangeEvent } from 'react'
import { Camera, ImageUp, LoaderCircle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

interface ProfileMediaUploadButtonProps {
  inputId: string
  mediaKind: 'avatar' | 'cover'
  isUploading: boolean
  disabled?: boolean
  compact?: boolean
  className?: string
  onUpload: (file: File) => Promise<unknown>
}

/** 通过可访问按钮打开系统文件选择器，并允许再次选择同一个文件。 */
export function ProfileMediaUploadButton({
  inputId,
  mediaKind,
  isUploading,
  disabled = false,
  compact = false,
  className,
  onUpload,
}: ProfileMediaUploadButtonProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const label = mediaKind === 'avatar' ? '更换头像' : '更换封面'
  const Icon = mediaKind === 'avatar' ? Camera : ImageUp

  async function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''

    if (file) {
      await onUpload(file)
    }
  }

  return (
    <>
      <input
        ref={inputRef}
        id={inputId}
        className="hidden"
        type="file"
        name={inputId}
        accept="image/jpeg,image/png,image/webp"
        onChange={(event) => void handleFileChange(event)}
        disabled={disabled || isUploading}
      />
      <Button
        type="button"
        variant="outline"
        size={compact ? 'icon' : 'default'}
        className={cn(className)}
        onClick={() => inputRef.current?.click()}
        disabled={disabled || isUploading}
        aria-label={label}
        aria-busy={isUploading}
        title={label}
      >
        {isUploading ? (
          <LoaderCircle className="size-4 animate-spin" aria-hidden="true" />
        ) : (
          <Icon className="size-4" aria-hidden="true" />
        )}
        {!compact && (isUploading ? '正在上传…' : label)}
      </Button>
    </>
  )
}
