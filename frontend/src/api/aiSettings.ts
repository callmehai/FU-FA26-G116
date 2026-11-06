import type { AiSettings, AiSettingsInput, AiSettingsSaved, AiTestResult } from '../types/aiSettings'
import { api } from './client'

/** ADR-0056 — the administrator's "Cấu hình AI" screen. The backend rechecks AdminUsers on every request. */
export const getAiSettings = () => api.get<AiSettings>('/api/admin/ai')

export const saveAiSettings = (body: AiSettingsInput) => api.put<AiSettingsSaved>('/api/admin/ai', body)

/** One short call to the API as the form stands; saves nothing. */
export const testAiSettings = (body: AiSettingsInput) => api.post<AiTestResult>('/api/admin/ai/test', body)
