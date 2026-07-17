import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

export function Panel({ className, ...props }: ComponentProps<'section'>) {
  return (
    <section
      className={cn(
        'flex h-full min-w-0 flex-col overflow-hidden bg-surface',
        className,
      )}
      {...props}
    />
  )
}
