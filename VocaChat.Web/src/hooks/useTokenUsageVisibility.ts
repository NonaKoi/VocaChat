import { useCallback, useSyncExternalStore } from 'react'

const storageKey = 'vocachat.show-token-usage'
const listeners = new Set<() => void>()

function readVisibility(): boolean {
  return window.localStorage.getItem(storageKey) === 'true'
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener)

  function handleStorage(event: StorageEvent) {
    if (event.key === storageKey) listener()
  }

  window.addEventListener('storage', handleStorage)
  return () => {
    listeners.delete(listener)
    window.removeEventListener('storage', handleStorage)
  }
}

function emitChange() {
  listeners.forEach((listener) => listener())
}

/**
 * 保存只影响当前客户端显示的 Token 明细偏好，不改变后端计量行为。
 */
export function useTokenUsageVisibility() {
  const isVisible = useSyncExternalStore(
    subscribe,
    readVisibility,
    () => false,
  )

  const setIsVisible = useCallback((value: boolean) => {
    window.localStorage.setItem(storageKey, String(value))
    emitChange()
  }, [])

  return { isVisible, setIsVisible }
}
