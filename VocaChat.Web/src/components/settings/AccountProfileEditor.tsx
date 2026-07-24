import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from 'react'
import { LoaderCircle, RotateCcw, Save } from 'lucide-react'
import type {
  AiAccountResponse,
  UpdateAiAccountRequest,
} from '@/api/types'
import { ProfileMediaUploadButton } from '@/components/aiAccounts/ProfileMediaUploadButton'
import { ProfileTagInput } from '@/components/aiAccounts/ProfileTagInput'
import { EntityAvatar } from '@/components/common/EntityAvatar'
import { Button } from '@/components/ui/button'
import { CharacterWorldSection } from '@/components/settings/CharacterWorldSection'
import type { CharacterWorldsState } from '@/hooks/useCharacterWorlds'

interface AccountProfileEditorProps {
  account: AiAccountResponse
  isSaving: boolean
  isUploadingAvatar: boolean
  isUploadingCover: boolean
  saveErrorMessage?: string
  mediaErrorMessage?: string
  characterWorlds: CharacterWorldsState
  worldUsageCounts: ReadonlyMap<string, number>
  onSave: (
    request: UpdateAiAccountRequest,
  ) => Promise<AiAccountResponse | undefined>
  onUploadAvatar: (file: File) => Promise<unknown>
  onUploadCover: (file: File) => Promise<unknown>
  onWorldChanged: () => void | Promise<void>
  onDirtyChange: (isDirty: boolean) => void
}

/** 编辑一个账号的公开资料；草稿仅在保存成功后写入后端。 */
export function AccountProfileEditor({
  account,
  isSaving,
  isUploadingAvatar,
  isUploadingCover,
  saveErrorMessage,
  mediaErrorMessage,
  characterWorlds,
  worldUsageCounts,
  onSave,
  onUploadAvatar,
  onUploadCover,
  onWorldChanged,
  onDirtyChange,
}: AccountProfileEditorProps) {
  const [baseline, setBaseline] = useState<UpdateAiAccountRequest>(() => toDraft(account))
  const [draft, setDraft] = useState<UpdateAiAccountRequest>(() => toDraft(account))
  const [hasWorldEditorChanges, setHasWorldEditorChanges] = useState(false)
  const isDirty = useMemo(
    () => JSON.stringify(draft) !== JSON.stringify(baseline),
    [baseline, draft],
  )
  const canSave = isDirty && !isSaving && draft.nickname.trim().length > 0

  useEffect(() => {
    onDirtyChange(isDirty || hasWorldEditorChanges)
  }, [hasWorldEditorChanges, isDirty, onDirtyChange])

  function updateValue<Key extends keyof UpdateAiAccountRequest>(
    key: Key,
    value: UpdateAiAccountRequest[Key],
  ) {
    setDraft((current) => ({ ...current, [key]: value }))
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canSave) return

    const updatedAccount = await onSave(draft)
    if (!updatedAccount) return

    const savedDraft = toDraft(updatedAccount)
    setBaseline(savedDraft)
    setDraft(savedDraft)
  }

  return (
    <form
      className="min-w-0"
      aria-labelledby="account-profile-editor-title"
      onSubmit={(event) => void handleSubmit(event)}
    >
      <div className="relative h-32 overflow-hidden border-b border-border bg-primary-soft">
        {account.coverUrl ? (
          <img
            src={account.coverUrl}
            alt=""
            className="h-full w-full object-cover"
            width={1200}
            height={320}
          />
        ) : (
          <div className="h-full bg-[linear-gradient(120deg,var(--vc-primary-soft),var(--vc-surface-muted))]" />
        )}
        <ProfileMediaUploadButton
          inputId={`settings-cover-${account.id}`}
          mediaKind="cover"
          isUploading={isUploadingCover}
          disabled={isUploadingAvatar}
          className="absolute top-4 right-4 border-white/45 bg-surface/95"
          onUpload={onUploadCover}
        />
      </div>

      <header className="flex min-w-0 items-end gap-4 border-b border-border px-5 pt-4 pb-5 sm:px-6">
        <span className="relative -mt-12 shrink-0">
          <EntityAvatar
            name={account.nickname}
            src={account.avatarUrl}
            size="large"
            className="size-20 rounded-xl ring-4 ring-surface"
          />
          <ProfileMediaUploadButton
            inputId={`settings-avatar-${account.id}`}
            mediaKind="avatar"
            isUploading={isUploadingAvatar}
            disabled={isUploadingCover}
            compact
            className="absolute -right-2 -bottom-2 size-8 rounded-full bg-surface shadow-message"
            onUpload={onUploadAvatar}
          />
        </span>
        <div className="min-w-0 flex-1 pb-0.5">
          <h3 id="account-profile-editor-title" className="truncate text-lg font-semibold text-foreground">
            {account.nickname}
          </h3>
          <p className="mt-1 truncate text-xs text-muted-foreground">
            VC号：{account.vcNumber}
          </p>
        </div>
        <p className="hidden text-xs text-muted-foreground sm:block">
          图片选择后会立即保存
        </p>
      </header>

      {(saveErrorMessage || mediaErrorMessage) && (
        <div className="mx-5 mt-5 rounded-lg border border-destructive/20 bg-danger-soft px-4 py-3 text-sm text-destructive sm:mx-6" role="alert">
          {saveErrorMessage ?? mediaErrorMessage}
        </div>
      )}

      <div className="grid gap-0 px-5 sm:px-6">
        <EditorSection title="基本身份" description="这些内容会显示在好友列表和资料页中。">
          <div className="grid gap-5 xl:grid-cols-2">
            <EditorField label="昵称" htmlFor="profile-nickname" required>
              <input
                id="profile-nickname"
                name="nickname"
                autoComplete="off"
                className="form-control"
                value={draft.nickname}
                maxLength={50}
                required
                aria-invalid={draft.nickname.trim().length === 0}
                onChange={(event) => updateValue('nickname', event.target.value)}
              />
            </EditorField>
            <EditorField label="VC号" htmlFor="profile-vc-number" description="支持英文、数字和特殊符号。">
              <input
                id="profile-vc-number"
                name="vcNumber"
                autoComplete="off"
                spellCheck={false}
                className="form-control"
                value={draft.vcNumber}
                maxLength={32}
                onChange={(event) => updateValue('vcNumber', event.target.value)}
              />
            </EditorField>
          </div>
          <div className="grid gap-5 xl:grid-cols-3">
            <EditorField label="生日" htmlFor="profile-birthday">
              <input
                id="profile-birthday"
                name="birthday"
                autoComplete="off"
                type="date"
                className="form-control"
                value={draft.birthday}
                onChange={(event) => updateValue('birthday', event.target.value)}
              />
            </EditorField>
            <EditorField label="性别" htmlFor="profile-gender">
              <select
                id="profile-gender"
                name="gender"
                autoComplete="off"
                className="form-control"
                value={draft.gender}
                onChange={(event) => updateValue('gender', event.target.value as UpdateAiAccountRequest['gender'])}
              >
                <option value="Unspecified">未设置</option>
                <option value="Male">男</option>
                <option value="Female">女</option>
                <option value="Other">其他</option>
              </select>
            </EditorField>
            <EditorField label="在线状态" htmlFor="profile-online-status">
              <select
                id="profile-online-status"
                name="onlineStatus"
                autoComplete="off"
                className="form-control"
                value={draft.onlineStatus}
                onChange={(event) => updateValue('onlineStatus', event.target.value as UpdateAiAccountRequest['onlineStatus'])}
              >
                <option value="Offline">离线</option>
                <option value="Online">在线</option>
                <option value="Away">离开</option>
                <option value="Busy">忙碌</option>
              </select>
            </EditorField>
          </div>
        </EditorSection>

        <EditorSection
          title="角色世界"
          description="决定这位好友理解环境、经历和长期事实时采用的基础设定。"
        >
          <CharacterWorldSection
            currentWorld={account.characterWorld}
            selectedWorldId={
              draft.characterWorldId ?? account.characterWorldId
            }
            worlds={characterWorlds.data}
            worldUsageCounts={worldUsageCounts}
            status={characterWorlds.status}
            errorMessage={characterWorlds.errorMessage}
            mutationErrorMessage={characterWorlds.mutationErrorMessage}
            isCreating={characterWorlds.isCreating}
            updatingWorldId={characterWorlds.updatingWorldId}
            onSelectWorld={(worldId) => updateValue(
              'characterWorldId',
              worldId,
            )}
            onReload={characterWorlds.reload}
            onCreate={characterWorlds.create}
            onUpdate={characterWorlds.update}
            onWorldChanged={onWorldChanged}
            onClearMutationError={characterWorlds.clearMutationError}
            onDirtyChange={setHasWorldEditorChanges}
          />
        </EditorSection>

        <EditorSection title="生活资料" description="用于好友资料中的基础信息展示。">
          <div className="grid gap-5 xl:grid-cols-3">
            <EditorField label="所在地" htmlFor="profile-location">
              <input id="profile-location" name="location" autoComplete="off" className="form-control" value={draft.location} maxLength={100} onChange={(event) => updateValue('location', event.target.value)} />
            </EditorField>
            <EditorField label="职业" htmlFor="profile-occupation">
              <input id="profile-occupation" name="occupation" autoComplete="off" className="form-control" value={draft.occupation} maxLength={100} onChange={(event) => updateValue('occupation', event.target.value)} />
            </EditorField>
            <EditorField label="故乡" htmlFor="profile-hometown">
              <input id="profile-hometown" name="hometown" autoComplete="off" className="form-control" value={draft.hometown} maxLength={100} onChange={(event) => updateValue('hometown', event.target.value)} />
            </EditorField>
          </div>
        </EditorSection>

        <EditorSection title="个人表达" description="明确对方是谁，以及长期交流时的表达方式。">
          <EditorTextArea id="profile-signature" label="个性签名" value={draft.signature} maxLength={200} rows={2} onChange={(value) => updateValue('signature', value)} />
          <EditorTextArea id="profile-identity" label="身份描述" value={draft.identityDescription} maxLength={500} rows={3} onChange={(value) => updateValue('identityDescription', value)} />
          <div className="grid gap-5 xl:grid-cols-2">
            <EditorTextArea id="profile-personality" label="性格描述" value={draft.personality} maxLength={500} rows={4} onChange={(value) => updateValue('personality', value)} />
            <EditorTextArea id="profile-speaking-style" label="说话风格" value={draft.speakingStyle} maxLength={500} rows={4} onChange={(value) => updateValue('speakingStyle', value)} />
          </div>
        </EditorSection>

        <EditorSection title="标签" description="标签以独立数据保存，可用于后续筛选与理解。">
          <div className="grid gap-6 xl:grid-cols-2">
            <ProfileTagInput id="profile-interest-tags" label="兴趣标签" description="记录长期兴趣和偏好。" values={draft.interestTags} onChange={(value) => updateValue('interestTags', value)} />
            <ProfileTagInput id="profile-personality-tags" label="个性标签" description="概括稳定的性格特征。" values={draft.personalityTags} onChange={(value) => updateValue('personalityTags', value)} />
          </div>
        </EditorSection>
      </div>

      <footer className="sticky bottom-0 flex flex-wrap items-center justify-between gap-3 border-t border-border bg-surface px-5 py-4 sm:px-6">
        <p className="text-xs text-muted-foreground" aria-live="polite">
          {isDirty ? '有尚未保存的资料修改' : '账号资料已保存'}
        </p>
        <div className="flex gap-2">
          <Button
            type="button"
            variant="outline"
            disabled={!isDirty || isSaving}
            onClick={() => setDraft(baseline)}
          >
            <RotateCcw className="size-4" aria-hidden="true" />
            撤销修改
          </Button>
          <Button type="submit" disabled={!canSave} aria-busy={isSaving}>
            {isSaving ? <LoaderCircle className="size-4 animate-spin" aria-hidden="true" /> : <Save className="size-4" aria-hidden="true" />}
            {isSaving ? '正在保存…' : '保存资料'}
          </Button>
        </div>
      </footer>
    </form>
  )
}

function EditorSection({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return (
    <section className="grid gap-5 border-b border-border py-6 last:border-b-0">
      <header>
        <h4 className="text-sm font-semibold text-foreground">{title}</h4>
        <p className="mt-1 text-xs leading-5 text-muted-foreground">{description}</p>
      </header>
      {children}
    </section>
  )
}

function EditorField({ label, htmlFor, description, required = false, children }: { label: string; htmlFor: string; description?: string; required?: boolean; children: ReactNode }) {
  return (
    <div className="grid content-start gap-2">
      <label
        className={`text-sm font-semibold text-foreground ${required ? "after:ml-1 after:text-destructive after:content-['*']" : ''}`}
        htmlFor={htmlFor}
      >
        {label}
      </label>
      {description && <span className="text-xs leading-5 text-muted-foreground">{description}</span>}
      {children}
    </div>
  )
}

function EditorTextArea({ id, label, value, maxLength, rows, onChange }: { id: string; label: string; value: string; maxLength: number; rows: number; onChange: (value: string) => void }) {
  return (
    <EditorField label={label} htmlFor={id}>
      <textarea id={id} name={id} autoComplete="off" className="form-control min-h-20 resize-y" value={value} maxLength={maxLength} rows={rows} onChange={(event) => onChange(event.target.value)} />
      <span className="justify-self-end text-xs text-muted-foreground">{value.length}/{maxLength}</span>
    </EditorField>
  )
}

function toDraft(account: AiAccountResponse): UpdateAiAccountRequest {
  return {
    nickname: account.nickname,
    vcNumber: account.vcNumber,
    identityDescription: account.identityDescription,
    personality: account.personality,
    speakingStyle: account.speakingStyle,
    signature: account.signature,
    birthday: account.birthday ?? '',
    gender: account.gender,
    location: account.location,
    occupation: account.occupation,
    hometown: account.hometown,
    onlineStatus: account.onlineStatus,
    characterWorldId: account.characterWorldId,
    interestTags: [...account.interestTags],
    personalityTags: [...account.personalityTags],
  }
}
