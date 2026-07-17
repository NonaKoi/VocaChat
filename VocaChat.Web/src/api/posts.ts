import { deleteJson, getJson, postJson, putJson } from '@/api/http'
import type { PostResponse } from '@/api/types'

export function getPosts(): Promise<PostResponse[]> { return getJson('/api/posts') }
export function likePost(id: string): Promise<PostResponse> { return putJson(`/api/posts/${id}/like`, {}) }
export function unlikePost(id: string): Promise<PostResponse> { return deleteJson(`/api/posts/${id}/like`) }
export function addPostComment(id: string, content: string): Promise<PostResponse> {
  return postJson(`/api/posts/${id}/comments`, { content })
}
