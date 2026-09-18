/**
 * Shared HTTP layer for the whole frontend.
 *
 * Paths are always relative (`/api/...`): in development Vite proxies them to the backend on
 * :5225, and in production the backend serves the frontend itself, so it is the same origin.
 * That means no base-URL environment variable and no CORS.
 */

/** A non-2xx response from the backend. Keeps the status so callers can decide how to react. */
export class ApiError extends Error {
  readonly status: number
  readonly url: string

  constructor(status: number, url: string, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.url = url
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(path, {
    ...init,
    headers: {
      Accept: 'application/json',
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
  })

  if (!res.ok) {
    throw new ApiError(res.status, path, `${init?.method ?? 'GET'} ${path} → HTTP ${res.status}`)
  }

  // 204 No Content has no body to parse.
  if (res.status === 204) return undefined as T

  return (await res.json()) as T
}

export const api = {
  get: <T>(path: string, init?: RequestInit) => request<T>(path, { ...init, method: 'GET' }),

  post: <T>(path: string, body?: unknown, init?: RequestInit) =>
    request<T>(path, { ...init, method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),

  put: <T>(path: string, body?: unknown, init?: RequestInit) =>
    request<T>(path, { ...init, method: 'PUT', body: body === undefined ? undefined : JSON.stringify(body) }),

  delete: <T>(path: string, init?: RequestInit) => request<T>(path, { ...init, method: 'DELETE' }),
}
