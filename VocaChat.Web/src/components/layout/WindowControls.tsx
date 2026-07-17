import { Minus, Square, X } from 'lucide-react'
import { getWindowHost } from '@/platform/windowHost'

export function WindowControls() {
  const host = getWindowHost()
  const actions = [{ label: '最小化', icon: Minus, run: () => host?.minimize() }, { label: '最大化', icon: Square, run: () => host?.maximize() }, { label: '关闭', icon: X, run: () => host?.close() }]
  return <div className="absolute top-2.5 right-3 z-40 hidden items-center gap-0.5 xl:flex" role="group" aria-label="窗口控制">{actions.map(({ label, icon: Icon, run }) => <button key={label} type="button" disabled={!host} title={host ? label : `${label}（本地应用中可用）`} aria-label={label} onClick={run} className="grid size-8 place-items-center rounded-md text-muted-foreground outline-none hover:bg-black/5 focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-35"><Icon className="size-4" aria-hidden="true" /></button>)}</div>
}
