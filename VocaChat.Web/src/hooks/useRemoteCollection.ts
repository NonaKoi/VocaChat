import { useCallback, useEffect, useState } from 'react'
import type { RemoteStatus } from '@/types/remoteStatus'

export function useRemoteCollection<T>(load: () => Promise<T[]>) {
  const [data, setData] = useState<T[]>([])
  const [status, setStatus] = useState<RemoteStatus>('loading')
  const [errorMessage, setErrorMessage] = useState<string>()
  const reload = useCallback(async () => {
    setStatus('loading'); setErrorMessage(undefined)
    try {
      const loadedData = await load()
      setData(loadedData)
      setStatus('success')
      return loadedData
    } catch (error) {
      setStatus('error')
      setErrorMessage(error instanceof Error ? error.message : '数据加载失败。')
      return undefined
    }
  }, [load])
  useEffect(() => { void reload() }, [reload])
  return { data, setData, status, errorMessage, reload }
}
