import { useCallback, useState } from 'react'
import { addPostComment, getPosts, likePost, unlikePost } from '@/api/posts'
import { useRemoteCollection } from '@/hooks/useRemoteCollection'

export function usePosts() {
  const state = useRemoteCollection(useCallback(() => getPosts(), []))
  const [actionError, setActionError] = useState<string>()
  async function update(id: string, action: () => Promise<Awaited<ReturnType<typeof likePost>>>) {
    setActionError(undefined)
    try {
      const post = await action()
      state.setData((items) => items.map((item) => item.id === id ? post : item))
    } catch (error) { setActionError(error instanceof Error ? error.message : '操作失败。') }
  }
  return {
    ...state,
    actionError,
    toggleLike: (id: string, liked: boolean) => update(id, () => liked ? unlikePost(id) : likePost(id)),
    addComment: (id: string, content: string) => update(id, () => addPostComment(id, content)),
  }
}
