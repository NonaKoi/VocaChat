/** 保持前端草稿与后端 1 至 4 条的业务边界一致。 */
export function isReplyMessageCountRangeValid(
  minimum: number,
  maximum: number,
) {
  return Number.isInteger(minimum)
    && Number.isInteger(maximum)
    && minimum >= 1
    && maximum <= 4
    && minimum <= maximum
}
