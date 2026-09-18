import type { HealthStatus } from '../types/health'
import { api } from './client'

export const getHealth = () => api.get<HealthStatus>('/api/health')
