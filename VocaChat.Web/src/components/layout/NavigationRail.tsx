import {
  Activity,
  Bookmark,
  ChevronDown,
  Folder,
  MessageCircleMore,
  MessagesSquare,
  Settings,
  UserRound,
} from 'lucide-react'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { NavigationItem } from '@/components/layout/NavigationItem'
import type { AppSection } from '@/types/appSection'

const primaryNavigation = [
  { section: 'chat', label: '聊天', icon: MessageCircleMore, enabled: true },
  { section: 'friends', label: '好友', icon: UserRound, enabled: false },
  { section: 'activity', label: '动态', icon: Activity, enabled: false },
] as const

const secondaryNavigation = [
  { section: 'files', label: '文件', icon: Folder },
  { section: 'favorites', label: '收藏', icon: Bookmark },
  { section: 'settings', label: '设置', icon: Settings },
] as const

interface NavigationRailProps {
  activeSection: AppSection
  onSectionChange?: (section: AppSection) => void
}

/** 复用示范资料中的桌面即时通讯主导航结构。 */
export function NavigationRail({
  activeSection,
  onSectionChange,
}: NavigationRailProps) {
  return (
    <nav
      className="flex h-full flex-col bg-navigation px-2.5 py-5 text-white xl:px-4"
      aria-label="主要功能"
    >
      <div className="flex items-center justify-center gap-3 px-1 xl:justify-start">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-primary text-white shadow-sm">
          <MessagesSquare className="size-5" strokeWidth={2} aria-hidden="true" />
        </span>
        <strong className="hidden text-base font-semibold tracking-[-0.01em] xl:block">
          VocaChat
        </strong>
      </div>

      <div className="mt-16 grid gap-2">
        {primaryNavigation.map((item) => (
          <NavigationItem
            key={item.section}
            section={item.section}
            label={item.label}
            icon={item.icon}
            active={activeSection === item.section}
            disabled={!item.enabled}
            onSelect={onSectionChange}
          />
        ))}
      </div>

      <div className="my-8 h-px bg-white/10" aria-hidden="true" />

      <div className="grid gap-2">
        {secondaryNavigation.map((item) => (
          <NavigationItem
            key={item.section}
            section={item.section}
            label={item.label}
            icon={item.icon}
            active={activeSection === item.section}
            disabled
            onSelect={onSectionChange}
          />
        ))}
      </div>

      <div className="mt-auto flex items-center justify-center gap-3 rounded-lg px-1 py-2 xl:justify-start">
        <span className="relative shrink-0">
          <EntityAvatar name="我" size="small" className="ring-2 ring-white/20" />
          <span className="absolute right-0 bottom-0 size-2.5 rounded-full border-2 border-navigation bg-emerald-400" />
        </span>
        <span className="hidden min-w-0 flex-1 xl:grid">
          <strong className="truncate text-sm font-medium text-white">本地用户</strong>
          <span className="text-xs text-navigation-muted">本地模式</span>
        </span>
        <ChevronDown className="hidden size-4 text-navigation-muted xl:block" aria-hidden="true" />
      </div>
    </nav>
  )
}
