import { useEffect, useMemo, useState } from 'react'
import { ChatWorkspace } from '@/components/chat/ChatWorkspace'
import { ConversationList } from '@/components/chat/ConversationList'
import { AppShell } from '@/components/layout/AppShell'
import { NavigationRail } from '@/components/layout/NavigationRail'
import { useGroupChats } from '@/hooks/useGroupChats'
import { useGroupMessages } from '@/hooks/useGroupMessages'

/** 组合聊天页的导航、会话列表和群消息工作区。 */
export function ChatPage() {
  const [selectedGroupChatId, setSelectedGroupChatId] = useState<string>()
  const [drafts, setDrafts] = useState<Record<string, string>>({})
  const groupChatsState = useGroupChats()
  const selectedGroupChat = useMemo(
    () =>
      groupChatsState.data.find(
        (groupChat) => groupChat.id === selectedGroupChatId,
      ),
    [groupChatsState.data, selectedGroupChatId],
  )
  const groupMessagesState = useGroupMessages(selectedGroupChat?.id)

  useEffect(() => {
    if (selectedGroupChatId && !selectedGroupChat) {
      setSelectedGroupChatId(undefined)
    }
  }, [selectedGroupChat, selectedGroupChatId])

  return (
    <AppShell
      navigation={<NavigationRail activeSection="chat" />}
      listPanel={
        <ConversationList
          groupChats={groupChatsState.data}
          status={groupChatsState.status}
          selectedId={selectedGroupChatId}
          errorMessage={groupChatsState.errorMessage}
          onSelect={setSelectedGroupChatId}
          onRetry={groupChatsState.reload}
        />
      }
      contentPanel={
        <ChatWorkspace
          groupChat={selectedGroupChat}
          messages={groupMessagesState.data}
          messageStatus={groupMessagesState.status}
          messageError={groupMessagesState.errorMessage}
          sendError={groupMessagesState.sendErrorMessage}
          isSending={groupMessagesState.isSending}
          draft={selectedGroupChat ? drafts[selectedGroupChat.id] ?? '' : ''}
          onDraftChange={(draft) => {
            if (!selectedGroupChat) {
              return
            }

            setDrafts((current) => ({
              ...current,
              [selectedGroupChat.id]: draft,
            }))
          }}
          onReloadMessages={groupMessagesState.reload}
          onSendMessage={groupMessagesState.send}
        />
      }
    />
  )
}
