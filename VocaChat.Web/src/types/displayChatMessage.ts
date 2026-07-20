import type { ChatMessageResponse } from '@/api/types'

/** 只属于界面展示的发送状态，不进入后端消息契约。 */
export interface DisplayChatMessage extends Omit<ChatMessageResponse, 'sequenceNumber'> {
  sequenceNumber: number | null
  deliveryStatus?: 'Sending'
}
