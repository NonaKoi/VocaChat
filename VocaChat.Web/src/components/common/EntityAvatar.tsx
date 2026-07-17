import { useEffect, useState } from 'react'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { cn } from '@/lib/utils'

const avatarTones = [
  'bg-avatar-blue',
  'bg-avatar-teal',
  'bg-avatar-slate',
  'bg-avatar-indigo',
] as const

interface EntityAvatarProps {
  name: string
  label?: string
  size?: 'small' | 'medium' | 'large'
  className?: string
  src?: string | null
  loading?: 'eager' | 'lazy'
}

function getStableTone(name: string): (typeof avatarTones)[number] {
  const hash = Array.from(name).reduce(
    (total, character) => total + (character.codePointAt(0) ?? 0),
    0,
  )

  return avatarTones[hash % avatarTones.length]
}

/** 使用名称生成稳定的首字头像，避免每次渲染出现随机颜色。 */
export function EntityAvatar({
  name,
  label,
  size = 'medium',
  className,
  src,
  loading = 'lazy',
}: EntityAvatarProps) {
  const [imageFailed, setImageFailed] = useState(false)

  useEffect(() => {
    setImageFailed(false)
  }, [src])

  const sizeClass = {
    small: 'size-8 rounded-md text-xs',
    medium: 'size-11 text-sm',
    large: 'size-20 rounded-xl text-2xl',
  }[size]

  return (
    <Avatar className={cn(sizeClass, className)} aria-hidden="true">
      {src && !imageFailed && (
        <AvatarImage
          src={src}
          alt=""
          loading={loading}
          fetchPriority={loading === 'eager' ? 'high' : 'auto'}
          onError={() => setImageFailed(true)}
        />
      )}
      <AvatarFallback className={getStableTone(name)}>
        {(label ?? name.slice(0, 1)).toUpperCase()}
      </AvatarFallback>
    </Avatar>
  )
}
