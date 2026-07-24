import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ComponentProps } from 'react'
import { describe, expect, it, vi } from 'vitest'
import type { CharacterWorldResponse } from '@/api/types'
import { CharacterWorldSection } from '@/components/settings/CharacterWorldSection'

const realityWorld = createWorld('world-default', '现实世界')
const academyWorld = createWorld('world-academy', '基沃托斯')

describe('CharacterWorldSection', () => {
  it('选择已有世界并说明共享编辑的影响范围', async () => {
    const user = userEvent.setup()
    const selectWorld = vi.fn()

    renderSection({
      worlds: [realityWorld, academyWorld],
      worldUsageCounts: new Map([['world-default', 3]]),
      onSelectWorld: selectWorld,
    })

    expect(screen.getByText(
      '共有 3 位好友使用这个世界，修改说明会同时影响他们。',
    )).toBeInTheDocument()

    await user.selectOptions(
      screen.getByRole('combobox', { name: '当前角色世界' }),
      academyWorld.id,
    )

    expect(selectWorld).toHaveBeenCalledWith(academyWorld.id)
  })

  it('创建世界后选择新世界并通知外部刷新', async () => {
    const user = userEvent.setup()
    const selectWorld = vi.fn()
    const worldChanged = vi.fn()
    const create = vi.fn().mockResolvedValue(academyWorld)

    renderSection({
      onCreate: create,
      onSelectWorld: selectWorld,
      onWorldChanged: worldChanged,
    })

    await user.click(screen.getByRole('button', { name: '新建世界' }))
    await user.type(screen.getByLabelText('世界名称'), '基沃托斯')
    await user.type(
      screen.getByLabelText('世界设定说明'),
      '用户说明优先于模型已有知识。',
    )
    await user.click(screen.getByRole('button', { name: '保存世界' }))

    await waitFor(() => expect(create).toHaveBeenCalledWith({
      name: '基沃托斯',
      description: '用户说明优先于模型已有知识。',
    }))
    expect(selectWorld).toHaveBeenCalledWith(academyWorld.id)
    expect(worldChanged).toHaveBeenCalledOnce()
  })

  it('编辑世界时通过独立更新入口保存', async () => {
    const user = userEvent.setup()
    const update = vi.fn().mockResolvedValue({
      ...realityWorld,
      description: '更新后的现实世界说明。',
    })

    renderSection({ onUpdate: update })

    await user.click(screen.getByRole('button', { name: '编辑世界' }))
    const description = screen.getByLabelText('世界设定说明')
    await user.clear(description)
    await user.type(description, '更新后的现实世界说明。')
    await user.click(screen.getByRole('button', { name: '保存世界' }))

    await waitFor(() => expect(update).toHaveBeenCalledWith(
      realityWorld.id,
      {
        name: realityWorld.name,
        description: '更新后的现实世界说明。',
      },
    ))
  })

  it('加载时保留当前世界，并在失败后提供重新加载入口', async () => {
    const user = userEvent.setup()
    const reload = vi.fn()
    const { rerender } = renderSection({
      worlds: [],
      status: 'loading',
      onReload: reload,
    })

    expect(screen.getByRole('combobox', {
      name: '当前角色世界',
    })).toHaveValue(realityWorld.id)
    expect(screen.getByRole('status', {
      name: '正在加载角色世界',
    })).toBeInTheDocument()

    rerender(
      <CharacterWorldSection
        currentWorld={realityWorld}
        selectedWorldId={realityWorld.id}
        worlds={[]}
        worldUsageCounts={new Map()}
        status="error"
        errorMessage="角色世界暂时无法读取。"
        isCreating={false}
        onSelectWorld={vi.fn()}
        onReload={reload}
        onCreate={vi.fn()}
        onUpdate={vi.fn()}
        onWorldChanged={vi.fn()}
        onClearMutationError={vi.fn()}
        onDirtyChange={vi.fn()}
      />,
    )

    expect(screen.getByRole('alert')).toHaveTextContent(
      '角色世界暂时无法读取。',
    )
    await user.click(screen.getByRole('button', { name: '重新加载' }))
    expect(reload).toHaveBeenCalledOnce()
  })
})

function renderSection(
  overrides: Partial<ComponentProps<typeof CharacterWorldSection>> = {},
) {
  const props: ComponentProps<typeof CharacterWorldSection> = {
    currentWorld: realityWorld,
    selectedWorldId: realityWorld.id,
    worlds: [realityWorld],
    worldUsageCounts: new Map([['world-default', 1]]),
    status: 'success',
    isCreating: false,
    onSelectWorld: vi.fn(),
    onReload: vi.fn(),
    onCreate: vi.fn(),
    onUpdate: vi.fn(),
    onWorldChanged: vi.fn(),
    onClearMutationError: vi.fn(),
    onDirtyChange: vi.fn(),
    ...overrides,
  }

  return render(<CharacterWorldSection {...props} />)
}

function createWorld(id: string, name: string): CharacterWorldResponse {
  return {
    id,
    name,
    description: `${name}的用户设定说明。`,
    createdAt: '2026-07-23T00:00:00Z',
    updatedAt: '2026-07-23T00:00:00Z',
  }
}
