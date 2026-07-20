import { useState } from 'react'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { MessageComposer } from '@/components/chat/MessageComposer'
import type { MessageSendOutcome } from '@/types/messageSendOutcome'

describe('MessageComposer', () => {
  it('发送成功后清空草稿', async () => {
    const user = userEvent.setup()
    const send = vi.fn().mockResolvedValue('success')
    render(<ComposerHarness onSend={send} />)

    await user.type(screen.getByLabelText('消息内容'), '你好')
    await user.click(screen.getByRole('button', { name: '发送消息' }))

    await waitFor(() => expect(send).toHaveBeenCalledWith('你好'))
    expect(screen.getByLabelText('消息内容')).toHaveValue('')
  })

  it('提交后立即清空输入框，不等待好友回复完成', async () => {
    const user = userEvent.setup()
    let resolveSend: ((outcome: MessageSendOutcome) => void) | undefined
    const send = vi.fn().mockReturnValue(new Promise<MessageSendOutcome>((resolve) => {
      resolveSend = resolve
    }))
    render(<ComposerHarness onSend={send} />)

    await user.type(screen.getByLabelText('消息内容'), '立即显示')
    await user.click(screen.getByRole('button', { name: '发送消息' }))

    expect(send).toHaveBeenCalledWith('立即显示')
    expect(screen.getByLabelText('消息内容')).toHaveValue('')

    resolveSend?.('success')
    await waitFor(() => expect(send).toHaveBeenCalledTimes(1))
  })

  it('用户消息被拒绝时保留草稿', async () => {
    const user = userEvent.setup()
    const send = vi.fn().mockResolvedValue('rejected')
    render(<ComposerHarness onSend={send} />)

    await user.type(screen.getByLabelText('消息内容'), '稍后重试')
    await user.click(screen.getByRole('button', { name: '发送消息' }))

    await waitFor(() => expect(send).toHaveBeenCalledTimes(1))
    expect(screen.getByLabelText('消息内容')).toHaveValue('稍后重试')
  })

  it('用户消息已保存但 AI 回复失败时清空草稿，避免重复发送', async () => {
    const user = userEvent.setup()
    const send = vi.fn().mockResolvedValue('partial')
    render(<ComposerHarness onSend={send} />)

    await user.type(screen.getByLabelText('消息内容'), '已保存的消息')
    fireEvent.keyDown(screen.getByLabelText('消息内容'), {
      key: 'Enter',
      shiftKey: false,
    })

    await waitFor(() => expect(send).toHaveBeenCalledWith('已保存的消息'))
    expect(screen.getByLabelText('消息内容')).toHaveValue('')
  })

  it('Shift + Enter 只换行，不发送消息', async () => {
    const user = userEvent.setup()
    const send = vi.fn().mockResolvedValue('success')
    render(<ComposerHarness onSend={send} />)

    const input = screen.getByLabelText('消息内容')
    await user.type(input, '第一行{shift>}{enter}{/shift}第二行')

    expect(send).not.toHaveBeenCalled()
    expect(input).toHaveValue('第一行\n第二行')
  })
})

function ComposerHarness({
  onSend,
}: {
  onSend: (content: string) => Promise<MessageSendOutcome>
}) {
  const [value, setValue] = useState('')

  return (
    <MessageComposer
      value={value}
      disabled={false}
      isSending={false}
      onValueChange={setValue}
      onSend={onSend}
    />
  )
}
