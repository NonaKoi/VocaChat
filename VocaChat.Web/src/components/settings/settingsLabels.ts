import type { AutonomyLevel } from '@/api/types'

export function getLevelLabel(level: AutonomyLevel) {
  if (level === 'Low') return '低'
  if (level === 'High') return '高'
  return '适中'
}
