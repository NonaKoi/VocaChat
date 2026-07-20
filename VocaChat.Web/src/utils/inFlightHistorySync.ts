const INITIAL_SYNC_DELAY = 450
const MAXIMUM_SYNC_DELAY = 30_000
const BACKOFF_MULTIPLIER = 1.6

/**
 * 在长时间消息请求期间逐步放慢历史同步，既及时显示常规回复，也避免超长间隔持续高频请求。
 */
export function startInFlightHistorySync(
  synchronize: () => Promise<unknown>,
): () => void {
  let stopped = false
  let timer: number | undefined
  let nextDelay = INITIAL_SYNC_DELAY

  const schedule = () => {
    timer = window.setTimeout(async () => {
      await synchronize()
      if (stopped) return

      nextDelay = Math.min(
        Math.ceil(nextDelay * BACKOFF_MULTIPLIER),
        MAXIMUM_SYNC_DELAY,
      )
      schedule()
    }, nextDelay)
  }

  schedule()

  return () => {
    stopped = true
    if (timer !== undefined) window.clearTimeout(timer)
  }
}
