import type { ReactNode } from 'react'

interface AppShellProps {
  navigation: ReactNode
  listPanel: ReactNode
  contentPanel: ReactNode
}

export function AppShell({
  navigation,
  listPanel,
  contentPanel,
}: AppShellProps) {
  return (
    <div className="h-dvh bg-canvas p-2.5">
      <a
        href="#main-workspace"
        className="fixed top-3 left-3 z-50 -translate-y-20 rounded-md bg-primary px-3 py-2 text-sm font-semibold text-white transition-transform focus:translate-y-0 focus:outline-none focus-visible:ring-2 focus-visible:ring-white"
      >
        跳到聊天工作区
      </a>
      <main className="grid h-full min-h-0 grid-cols-[84px_320px_minmax(0,1fr)] overflow-hidden rounded-xl bg-surface shadow-shell xl:grid-cols-[190px_372px_minmax(0,1fr)]">
        <aside className="min-h-0 bg-navigation">{navigation}</aside>
        <section className="min-h-0 min-w-0 border-r border-border bg-surface">
          {listPanel}
        </section>
        <section
          id="main-workspace"
          tabIndex={-1}
          className="min-h-0 min-w-0 overflow-hidden bg-surface-muted outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/30"
        >
          {contentPanel}
        </section>
      </main>
    </div>
  )
}
