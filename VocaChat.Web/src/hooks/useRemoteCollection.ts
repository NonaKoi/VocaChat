import { useCallback, useEffect, useState } from 'react'
import type { RemoteStatus } from '@/types/remoteStatus'

export function useRemoteCollection<T>(load: () => Promise<T[]>) {
  const [data, setData] = useState<T[]>([])
  const [status, setStatus] = useState<RemoteStatus>('loading')
  const [errorMessage, setErrorMessage] = useState<string>()
  const reload = useCallback(async () => {
    setStatus('loading'); setErrorMessage(undefined)
    try { setData(await load()); setStatus('success') }
    catch (error) { setStatus('error'); setErrorMessage(error instanceof Error ? error.message : '数据加载失败。') }
  }, [load])
  useEffect(() => { void reload() }, [reload])
  return { data, setData, status, errorMessage, reload }
}
