import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { getAiSettings, saveAiSettings, testAiSettings } from '../../api/aiSettings'
import { ApiError } from '../../api/client'
import { Badge } from '../../components/Badge'
import { Icon } from '../../components/Icon'
import { InfoTip } from '../../components/InfoTip'
import { PageHeader } from '../../components/PageHeader'
import { SegmentedControl } from '../../components/SegmentedControl'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import type { AiProviderMode, AiSettings, AiSettingsInput } from '../../types/aiSettings'

type Field = 'mode' | 'apiBaseUrl' | 'apiModel' | 'apiKey'
type Errors = Partial<Record<Field, string>>

/** The server's `errors` dictionary: one message per field (it sends a list, the first one is enough). */
function fieldErrors(error: unknown): Errors {
  if (!(error instanceof ApiError)) return {}
  const raw = (error.body?.errors ?? {}) as Record<string, string | string[]>
  return Object.fromEntries(Object.entries(raw).map(([key, value]) => [key, Array.isArray(value) ? value[0] : value]))
}

const MODES: { value: AiProviderMode; label: string }[] = [
  { value: 'Local', label: 'Máy local' },
  { value: 'Api', label: 'API' },
]

/**
 * ADR-0056 — where the AI drafts of ordinary meetings are written: the in-house model (ADR-0054) or an OpenAI-compatible
 * API. Internal, Confidential and Secret meetings always stay on the in-house model, whatever is chosen here. The key
 * goes to the server and never comes back: the field stays empty and shows the stored key's last four characters.
 */
export function AiSettingsPage() {
  const toast = useToast()
  const [settings, setSettings] = useState<AiSettings | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [mode, setMode] = useState<AiProviderMode>('Local')
  const [baseUrl, setBaseUrl] = useState('')
  const [model, setModel] = useState('')
  const [key, setKey] = useState('')
  const [errors, setErrors] = useState<Errors>({})
  const [saving, setSaving] = useState(false)
  const [testing, setTesting] = useState(false)
  const [test, setTest] = useState<{ ok: boolean; text: string } | null>(null)

  const apply = useCallback((next: AiSettings) => {
    setSettings(next)
    setMode(next.mode)
    setBaseUrl(next.apiBaseUrl)
    setModel(next.apiModel)
    setKey('')
  }, [])

  const load = useCallback(async () => {
    try {
      apply(await getAiSettings())
      setLoadError(null)
    } catch (error) {
      setLoadError(errorMessage(error))
    }
  }, [apply])

  useEffect(() => {
    void Promise.resolve().then(load)
  }, [load])

  const input = (): AiSettingsInput => ({
    mode,
    apiBaseUrl: baseUrl,
    apiModel: model,
    apiKey: key.trim() ? key : null,
  })

  const clear = (field: Field) => setErrors((current) => ({ ...current, [field]: undefined }))

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setSaving(true)
    try {
      const result = await saveAiSettings(input())
      apply(result.settings)
      setErrors({})
      setTest(null)
      toast.show(result.message, 'success')
    } catch (error) {
      setErrors(fieldErrors(error))
      toast.show(errorMessage(error), 'error')
    } finally {
      setSaving(false)
    }
  }

  const runTest = async () => {
    setTesting(true)
    setTest(null)
    try {
      const result = await testAiSettings(input())
      setTest({ ok: true, text: `${result.message} · ${(result.elapsedMs / 1000).toFixed(1).replace('.', ',')} giây` })
    } catch (error) {
      if (isApiError(error, 400, 'MSG409')) setErrors(fieldErrors(error))
      setTest({ ok: false, text: errorMessage(error) })
    } finally {
      setTesting(false)
    }
  }

  if (loadError)
    return (
      <>
        <PageHeader title="Cấu hình AI" />
        <div className="alert alert-error page-alert" role="alert">
          <p>{loadError}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void load()}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      </>
    )

  const api = mode === 'Api'
  return (
    <>
      <PageHeader title="Cấu hình AI" description="Máy viết dự thảo biên bản" />
      {!settings ? (
        <div className="card card-pending ai-settings" aria-busy="true">
          <p>Đang tải…</p>
        </div>
      ) : (
        <form className="card ai-settings" onSubmit={submit} noValidate>
          <div className="ai-settings-mode">
            <span className="ai-settings-label" id="ai-mode-label">
              Viết dự thảo bằng
              <InfoTip text="Chỉ cuộc họp Thường dùng API. Cuộc họp Nội bộ, Mật, Tối mật luôn dùng máy local: lời thoại của chúng không rời server." />
            </span>
            <SegmentedControl
              label="Viết dự thảo bằng"
              value={mode}
              options={MODES}
              onChange={(next) => {
                setMode(next)
                setTest(null)
                clear('mode')
              }}
            />
          </div>

          <p className="ai-settings-local">
            <span>Máy local</span>
            <strong>{settings.localModel || 'Chưa cấu hình'}</strong>
            <Badge tone={settings.localConfigured ? 'emerald' : 'slate'}>
              {settings.localConfigured ? 'Đã cấu hình' : 'Chưa cấu hình'}
            </Badge>
          </p>

          {api && (
            <fieldset className="form-grid ai-settings-api">
              <legend className="ai-settings-label">API (chuẩn OpenAI)</legend>
              <label className="field span-2">
                <span className={api ? 'field-required' : undefined}>Địa chỉ API</span>
                <input
                  type="url"
                  inputMode="url"
                  value={baseUrl}
                  placeholder="https://opencode.ai/inference/go/v1"
                  onChange={(e) => {
                    setBaseUrl(e.target.value)
                    clear('apiBaseUrl')
                    setTest(null)
                  }}
                  aria-invalid={!!errors.apiBaseUrl}
                  autoComplete="off"
                  spellCheck={false}
                />
                {errors.apiBaseUrl && <small className="field-error">{errors.apiBaseUrl}</small>}
              </label>
              <label className="field">
                <span className={api ? 'field-required' : undefined}>Model</span>
                <input
                  value={model}
                  placeholder="deepseek-v4.1-flash"
                  onChange={(e) => {
                    setModel(e.target.value)
                    clear('apiModel')
                    setTest(null)
                  }}
                  aria-invalid={!!errors.apiModel}
                  autoComplete="off"
                  spellCheck={false}
                />
                {errors.apiModel && <small className="field-error">{errors.apiModel}</small>}
              </label>
              <label className="field">
                <span className={api && !settings.hasApiKey ? 'field-required' : undefined}>Key</span>
                <span className="ai-settings-key">
                  <input
                    type="password"
                    value={key}
                    placeholder={settings.hasApiKey ? `Đã lưu ••••${settings.apiKeyHint}` : 'Dán key'}
                    onChange={(e) => {
                      setKey(e.target.value)
                      clear('apiKey')
                      setTest(null)
                    }}
                    aria-invalid={!!errors.apiKey}
                    autoComplete="new-password"
                    spellCheck={false}
                  />
                </span>
                {errors.apiKey && <small className="field-error">{errors.apiKey}</small>}
              </label>
            </fieldset>
          )}

          {test && (
            <p className={test.ok ? 'alert alert-success' : 'alert alert-error'} role="status">
              <Icon name={test.ok ? 'checkCircle' : 'alertCircle'} size={16} />
              {test.text}
            </p>
          )}

          <div className="ai-settings-foot">
            {settings.updatedAt && (
              <small className="field-hint">
                {settings.updatedByName ? `${settings.updatedByName} · ` : ''}
                {formatDateTime(settings.updatedAt)}
              </small>
            )}
            <div className="form-actions">
              {api && (
                <button type="button" className="btn btn-outline" onClick={() => void runTest()} disabled={testing || saving}>
                  <Icon name="broadcast" size={16} />
                  {testing ? 'Đang thử…' : 'Thử kết nối'}
                </button>
              )}
              <button type="submit" className="btn btn-primary" disabled={saving || testing}>
                {saving ? 'Đang lưu…' : 'Lưu'}
              </button>
            </div>
          </div>
        </form>
      )}
    </>
  )
}
