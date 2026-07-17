import type { ComponentProps } from 'react'
import { Avatar as AvatarPrimitive } from 'radix-ui'
import { cn } from '@/lib/utils'

function Avatar({ className, ...props }: ComponentProps<typeof AvatarPrimitive.Root>) {
  return (
    <AvatarPrimitive.Root
      data-slot="avatar"
      className={cn(
        'relative flex shrink-0 overflow-hidden rounded-lg ring-1 ring-white/40',
        className,
      )}
      {...props}
    />
  )
}

function AvatarFallback({
  className,
  ...props
}: ComponentProps<typeof AvatarPrimitive.Fallback>) {
  return (
    <AvatarPrimitive.Fallback
      data-slot="avatar-fallback"
      className={cn(
        'flex size-full items-center justify-center font-display font-semibold text-white',
        className,
      )}
      {...props}
    />
  )
}

function AvatarImage({
  className,
  alt = '',
  width = 96,
  height = 96,
  ...props
}: ComponentProps<'img'>) {
  return (
    <img
      data-slot="avatar-image"
      className={cn('absolute inset-0 z-10 size-full object-cover', className)}
      alt={alt}
      width={width}
      height={height}
      {...props}
    />
  )
}

export { Avatar, AvatarFallback, AvatarImage }
