/** ADR-0056 — which model writes the AI drafts of ordinary meetings. Mirrors AiSettingsView. */
export type AiProviderMode = 'Local' | 'Api'

export interface AiSettings {
  mode: AiProviderMode
  apiBaseUrl: string
  apiModel: string
  /** Whether a key is stored; the key itself never reaches the browser. */
  hasApiKey: boolean
  /** The stored key's last four characters, or "". */
  apiKeyHint: string
  /** The in-house model the server is configured with (ADR-0054). */
  localModel: string
  localConfigured: boolean
  updatedAt: string | null
  updatedByName: string | null
}

/** `apiKey`: null keeps the stored key, "" removes it, anything else replaces it. */
export interface AiSettingsInput {
  mode: AiProviderMode
  apiBaseUrl: string
  apiModel: string
  apiKey: string | null
}

export interface AiSettingsSaved {
  code: string
  message: string
  settings: AiSettings
}

export interface AiTestResult {
  code: string
  message: string
  model: string
  elapsedMs: number
}
