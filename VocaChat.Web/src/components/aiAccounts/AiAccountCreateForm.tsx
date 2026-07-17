import { useEffect, useRef, type FormEvent, type ReactNode } from 'react'
import { LoaderCircle, ShieldCheck, UserRoundSearch } from 'lucide-react'
import type { AiAccountResponse, CreateAiAccountRequest } from '@/api/types'
import { ProfileTagInput } from '@/components/aiAccounts/ProfileTagInput'
import { Button } from '@/components/ui/button'

interface AiAccountCreateFormProps {
  values: CreateAiAccountRequest
  isSubmitting: boolean
  errorMessage?: string
  onValuesChange: (values: CreateAiAccountRequest) => void
  onCancel: () => void
  onCreate: (request: CreateAiAccountRequest) => Promise<AiAccountResponse | undefined>
}

/** 收集新朋友的完整档案，最终业务验证仍由后端 Service 负责。 */
export function AiAccountCreateForm({
  values,
  isSubmitting,
  errorMessage,
  onValuesChange,
  onCancel,
  onCreate,
}: AiAccountCreateFormProps) {
  const nicknameInputRef = useRef<HTMLInputElement>(null)
  const canSubmit = !isSubmitting && values.nickname.trim().length > 0

  useEffect(() => {
    nicknameInputRef.current?.focus()
  }, [])

  useEffect(() => {
    if (errorMessage) nicknameInputRef.current?.focus()
  }, [errorMessage])

  function updateValue<Key extends keyof CreateAiAccountRequest>(
    field: Key,
    value: CreateAiAccountRequest[Key],
  ): void {
    onValuesChange({ ...values, [field]: value })
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canSubmit) {
      nicknameInputRef.current?.focus()
      return
    }
    await onCreate(values)
  }

  return (
    <section className="h-full overflow-y-auto bg-surface-muted px-6 py-8 xl:px-10">
      <form
        className="mx-auto w-full max-w-3xl"
        onSubmit={(event) => void handleSubmit(event)}
        aria-labelledby="find-friend-title"
      >
        <header className="flex items-start gap-4 border-b border-border pb-6">
          <span className="grid size-12 shrink-0 place-items-center rounded-xl bg-primary-soft text-primary">
            <UserRoundSearch className="size-6" aria-hidden="true" />
          </span>
          <div className="min-w-0">
            <h1 id="find-friend-title" className="text-xl font-semibold text-foreground">
              寻找新朋友
            </h1>
            <p className="mt-1 max-w-2xl text-sm leading-6 text-muted-foreground">
              完善这位朋友的长期档案。添加后，对方会出现在好友列表中，并可以被邀请进入群聊。
            </p>
          </div>
        </header>

        {errorMessage && (
          <div
            id="find-friend-error"
            className="mt-5 rounded-lg border border-destructive/20 bg-danger-soft px-4 py-3 text-sm text-destructive"
            role="alert"
          >
            <strong className="font-semibold">添加失败：</strong>
            {errorMessage}
          </div>
        )}

        <div className="grid gap-8 py-7">
          <FormSection title="基本身份" description="用于好友列表和个人资料页的核心身份信息。">
            <div className="grid gap-6 lg:grid-cols-2">
              <TextField
                ref={nicknameInputRef}
                id="friend-nickname"
                label="昵称"
                description="为这位新朋友填写一个容易辨认的昵称。"
                value={values.nickname}
                onChange={(value) => updateValue('nickname', value)}
                maxLength={50}
                placeholder="例如：小语"
                required
                errorMessage={errorMessage}
              />
              <TextField
                id="friend-vc-number"
                label="VC号"
                description="可使用英文、数字和特殊符号；留空时自动生成 7 位随机数字号。"
                value={values.vcNumber}
                onChange={(value) => updateValue('vcNumber', value)}
                maxLength={32}
                placeholder="例如：Nona#2026（可留空）"
              />
            </div>

            <div className="grid gap-6 md:grid-cols-3">
              <Field label="生日" id="friend-birthday" description="年龄和星座会由生日自动计算。">
                <input
                  id="friend-birthday"
                  name="birthday"
                  type="date"
                  value={values.birthday}
                  onChange={(event) => updateValue('birthday', event.target.value)}
                  aria-describedby="friend-birthday-description"
                  className="form-control"
                />
              </Field>
              <Field label="性别" id="friend-gender" description="用于个人资料中的基础信息展示。">
                <select
                  id="friend-gender"
                  name="gender"
                  value={values.gender}
                  onChange={(event) =>
                    updateValue('gender', event.target.value as CreateAiAccountRequest['gender'])
                  }
                  aria-describedby="friend-gender-description"
                  className="form-control"
                >
                  <option value="Unspecified">未设置</option>
                  <option value="Male">男</option>
                  <option value="Female">女</option>
                  <option value="Other">其他</option>
                </select>
              </Field>
              <Field label="状态" id="friend-online-status" description="当前只作为本地好友档案状态展示。">
                <select
                  id="friend-online-status"
                  name="online-status"
                  value={values.onlineStatus}
                  onChange={(event) =>
                    updateValue('onlineStatus', event.target.value as CreateAiAccountRequest['onlineStatus'])
                  }
                  aria-describedby="friend-online-status-description"
                  className="form-control"
                >
                  <option value="Offline">离线</option>
                  <option value="Online">在线</option>
                  <option value="Away">离开</option>
                  <option value="Busy">忙碌</option>
                </select>
              </Field>
            </div>
          </FormSection>

          <FormSection title="生活资料" description="这些信息会组成好友资料页中的基础资料区。">
            <div className="grid gap-6 md:grid-cols-3">
              <TextField
                id="friend-location"
                label="所在地"
                description="例如：中国 上海。"
                value={values.location}
                onChange={(value) => updateValue('location', value)}
                maxLength={100}
                placeholder="当前生活的地方"
              />
              <TextField
                id="friend-occupation"
                label="职业"
                description="描述对方现在主要从事的事情。"
                value={values.occupation}
                onChange={(value) => updateValue('occupation', value)}
                maxLength={100}
                placeholder="例如：自由插画师"
              />
              <TextField
                id="friend-hometown"
                label="故乡"
                description="例如：中国 杭州。"
                value={values.hometown}
                onChange={(value) => updateValue('hometown', value)}
                maxLength={100}
                placeholder="对方长大的地方"
              />
            </div>
          </FormSection>

          <FormSection title="个人表达" description="说明对方是谁、如何表达，以及相处时给人的感受。">
            <TextAreaField
              id="friend-signature"
              label="个性签名"
              description="显示在资料页顶部的一句话。"
              value={values.signature}
              onChange={(value) => updateValue('signature', value)}
              maxLength={200}
              rows={2}
              placeholder="例如：在自己的节奏里，一步一步走下去。"
            />
            <TextAreaField
              id="friend-introduction"
              label="个人介绍"
              description="简单说明对方是谁，以及你们会如何相处。"
              value={values.identityDescription}
              onChange={(value) => updateValue('identityDescription', value)}
              maxLength={500}
              rows={3}
              placeholder="例如：喜欢阅读、擅长整理信息的学习伙伴"
            />
            <div className="grid gap-6 lg:grid-cols-2">
              <TextAreaField
                id="friend-personality"
                label="性格描述"
                description="用于 AI 后续保持稳定性格的详细描述。"
                value={values.personality}
                onChange={(value) => updateValue('personality', value)}
                maxLength={200}
                rows={4}
                placeholder="例如：耐心、好奇、重视事实"
              />
              <TextAreaField
                id="friend-speaking-style"
                label="说话风格"
                description="描述对方平时的表达方式。"
                value={values.speakingStyle}
                onChange={(value) => updateValue('speakingStyle', value)}
                maxLength={200}
                rows={4}
                placeholder="例如：简洁温和，必要时列出要点"
              />
            </div>
          </FormSection>

          <FormSection title="兴趣与个性" description="标签会作为独立数据保存，方便以后搜索和筛选。">
            <div className="grid gap-6 lg:grid-cols-2">
              <ProfileTagInput
                id="friend-interest-tags"
                label="兴趣标签"
                description="例如：绘画、阅读、咖啡。"
                values={values.interestTags}
                onChange={(tags) => updateValue('interestTags', tags)}
              />
              <ProfileTagInput
                id="friend-personality-tags"
                label="个性标签"
                description="例如：冷静、理性、可靠。"
                values={values.personalityTags}
                onChange={(tags) => updateValue('personalityTags', tags)}
              />
            </div>
          </FormSection>
        </div>

        <footer className="flex flex-wrap items-center justify-between gap-4 border-t border-border pt-5">
          <p className="flex items-center gap-2 text-xs text-muted-foreground">
            <ShieldCheck className="size-4" aria-hidden="true" />
            好友资料只保存在当前设备
          </p>
          <div className="flex items-center gap-2">
            <Button variant="ghost" onClick={onCancel} disabled={isSubmitting}>取消</Button>
            <Button type="submit" disabled={!canSubmit}>
              {isSubmitting && <LoaderCircle className="size-4 animate-spin" aria-hidden="true" />}
              {isSubmitting ? '正在添加…' : '寻找并添加为好友'}
            </Button>
          </div>
        </footer>
      </form>
    </section>
  )
}

function FormSection({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return (
    <section className="grid gap-5 border-b border-border pb-8 last:border-b-0 last:pb-0">
      <header>
        <h2 className="text-base font-semibold text-foreground">{title}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{description}</p>
      </header>
      {children}
    </section>
  )
}

function Field({ id, label, description, required = false, children }: { id: string; label: string; description: string; required?: boolean; children: ReactNode }) {
  return (
    <div className="grid content-start gap-2">
      <div className="flex items-baseline justify-between gap-3">
        <label htmlFor={id} className="text-sm font-semibold text-foreground">{label}</label>
        {required && <span className="text-xs text-destructive">必填</span>}
      </div>
      <p id={`${id}-description`} className="text-xs text-muted-foreground">{description}</p>
      {children}
    </div>
  )
}

interface TextFieldProps {
  id: string
  label: string
  description: string
  value: string
  onChange: (value: string) => void
  maxLength: number
  placeholder: string
  required?: boolean
  errorMessage?: string
  ref?: React.Ref<HTMLInputElement>
}

function TextField({ ref, id, label, description, value, onChange, maxLength, placeholder, required, errorMessage }: TextFieldProps) {
  return (
    <Field id={id} label={label} description={description} required={required}>
      <input
        ref={ref}
        id={id}
        name={id}
        autoComplete="off"
        maxLength={maxLength}
        required={required}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        aria-invalid={Boolean(errorMessage)}
        aria-describedby={`${id}-description${errorMessage ? ' find-friend-error' : ''}`}
        className="form-control"
      />
    </Field>
  )
}

function TextAreaField({ id, label, description, value, onChange, maxLength, rows, placeholder }: Omit<TextFieldProps, 'required' | 'errorMessage' | 'ref'> & { rows: number }) {
  return (
    <Field id={id} label={label} description={description}>
      <textarea
        id={id}
        name={id}
        autoComplete="off"
        rows={rows}
        maxLength={maxLength}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        aria-describedby={`${id}-description`}
        className="form-control resize-y"
      />
    </Field>
  )
}
