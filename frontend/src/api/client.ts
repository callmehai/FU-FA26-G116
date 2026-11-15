/**
 * Shared HTTP layer for the whole frontend.
 *
 * Paths are always relative (`/api/...`): in development Vite proxies them to the backend on
 * :5225, and in production the backend serves the frontend itself, so it is the same origin.
 * That means no base-URL environment variable and no CORS.
 *
 * Every request carries the session's access token unless it is marked `anonymous`. The tokens
 * themselves live in `session.ts`, which plugs in through `configureAuth` — this file only knows
 * how to ask for a token and what to do when the backend rejects one.
 */

/**
 * The JSON body the backend sends with most errors: a message code the UI matches on, a
 * Vietnamese message it can show verbatim, and, for validation failures, one message per field.
 *
 * Match on `code`, never on `message` — the wording may change, the code is the contract.
 */
export interface ApiErrorBody {
  code: string
  message: string
  errors?: Record<string, string>
  /** How long to wait before asking again, for a 429 (UC-04's MSG373 / MSG379). */
  retryAfterSeconds?: number
  /** With UC-04's MSG126: wrong codes the current code still takes before it locks (MSG372). */
  remainingAttempts?: number
}

/** A non-2xx response from the backend. Keeps the status and, when there was one, the body. */
export class ApiError extends Error {
  readonly status: number
  readonly url: string
  /** Absent when the response had no JSON body — 403, 404 and the JWT middleware's 401 send none. */
  readonly body?: ApiErrorBody

  constructor(status: number, url: string, message: string, body?: ApiErrorBody) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.url = url
    this.body = body
  }

  get code() {
    return this.body?.code
  }
}

/** Where authenticated requests get their bearer token from. Implemented by `session.ts`. */
export interface AuthHooks {
  /** A usable access token, refreshing first if it is about to expire; null when signed out. */
  getAccessToken: () => Promise<string | null>
  /** Called once when the backend rejects a token: refresh and return the new one, or null. */
  recoverFromUnauthorized: () => Promise<string | null>
}

let auth: AuthHooks | null = null
/**
 * A guest page's own token (UC-04). While one is on screen, every authenticated request and hub of this
 * tab goes as the guest, never with an internal session the same browser may also hold; the guest pages
 * set it on mount and clear it on unmount, and no internal screen is on screen at the same time.
 */
let guestAuth: AuthHooks | null = null

export function configureAuth(hooks: AuthHooks) {
  auth = hooks
}

export function setGuestAuth(hooks: AuthHooks | null) {
  guestAuth = hooks
}

const currentAuth = () => guestAuth ?? auth

/**
 * The session's access token, refreshed first when it is about to expire. For the realtime hubs,
 * which authenticate their own connection instead of going through `request`.
 */
export function getAccessToken(): Promise<string | null> {
  const hooks = currentAuth()
  return hooks ? hooks.getAccessToken() : Promise.resolve(null)
}

interface RequestOptions extends RequestInit {
  /** Send no bearer token and never try to refresh — for the sign-in endpoints themselves. */
  anonymous?: boolean
}

/**
 * Reads an error body if there is one. Several error responses are empty (`Content-Length: 0`),
 * and `JSON.parse` on an empty body throws, which would hide the real status behind a parse error.
 */
function parseErrorBody(text: string): ApiErrorBody | undefined {
  try {
    if (!text) return undefined
    const parsed = JSON.parse(text) as Partial<ApiErrorBody>
    return typeof parsed.code === 'string' ? (parsed as ApiErrorBody) : undefined
  } catch {
    return undefined
  }
}

function apiError(status: number, path: string, method: string, text: string) {
  const body = parseErrorBody(text)
  return new ApiError(status, path, body?.message ?? `${method} ${path} → HTTP ${status}`, body)
}

async function send(path: string, init: RequestOptions, token: string | null) {
  const { anonymous: _anonymous, ...rest } = init
  return fetch(path, {
    ...rest,
    headers: {
      Accept: 'application/json',
      // A FormData body sets its own multipart Content-Type, boundary included.
      ...(typeof rest.body === 'string' ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...rest.headers,
    },
  })
}

/** One request with the session's token, refreshed and retried once on a 401; non-2xx throws `ApiError`. */
async function fetchOk(path: string, init: RequestOptions): Promise<Response> {
  const hooks = init.anonymous ? null : currentAuth()
  let res = await send(path, init, hooks ? await hooks.getAccessToken() : null)

  // An expired or revoked access token: refresh once and retry once. A second 401 means the
  // session is really gone, and `recoverFromUnauthorized` has already signed the user out.
  if (res.status === 401 && hooks) {
    const token = await hooks.recoverFromUnauthorized()
    if (token) res = await send(path, init, token)
  }

  if (!res.ok) throw apiError(res.status, path, init.method ?? 'GET', await res.text().catch(() => ''))
  return res
}

async function request<T>(path: string, init: RequestOptions): Promise<T> {
  const res = await fetchOk(path, init)

  // 204 No Content has no body to parse.
  if (res.status === 204) return undefined as T

  return (await res.json()) as T
}

/**
 * POSTs a `FormData` through XMLHttpRequest, which, unlike fetch, reports upload progress. The
 * browser sets the multipart boundary itself. Same token handling and errors as `request`; a
 * network failure rejects with a TypeError as fetch does, an abort with an `AbortError`.
 */
async function upload<T>(
  path: string,
  form: FormData,
  { onProgress, signal }: { onProgress?: (loaded: number, total: number) => void; signal?: AbortSignal } = {},
): Promise<T> {
  const attempt = (token: string | null) =>
    new Promise<XMLHttpRequest>((resolve, reject) => {
      const xhr = new XMLHttpRequest()
      xhr.open('POST', path)
      xhr.setRequestHeader('Accept', 'application/json')
      if (token) xhr.setRequestHeader('Authorization', `Bearer ${token}`)
      xhr.upload.onprogress = (e) => e.lengthComputable && onProgress?.(e.loaded, e.total)
      xhr.onload = () => resolve(xhr)
      xhr.onerror = () => reject(new TypeError('Network request failed'))
      xhr.onabort = () => reject(new DOMException('Upload cancelled', 'AbortError'))
      if (signal?.aborted) return reject(new DOMException('Upload cancelled', 'AbortError'))
      signal?.addEventListener('abort', () => xhr.abort(), { once: true })
      xhr.send(form)
    })

  const hooks = currentAuth()
  let xhr = await attempt(hooks ? await hooks.getAccessToken() : null)
  if (xhr.status === 401 && hooks) {
    const token = await hooks.recoverFromUnauthorized()
    if (token) xhr = await attempt(token)
  }
  if (xhr.status < 200 || xhr.status >= 300) throw apiError(xhr.status, path, 'POST', xhr.responseText)
  return JSON.parse(xhr.responseText) as T
}

export const api = {
  get: <T>(path: string, init?: RequestOptions) => request<T>(path, { ...init, method: 'GET' }),

  post: <T>(path: string, body?: unknown, init?: RequestOptions) =>
    request<T>(path, { ...init, method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),

  put: <T>(path: string, body?: unknown, init?: RequestOptions) =>
    request<T>(path, { ...init, method: 'PUT', body: body === undefined ? undefined : JSON.stringify(body) }),

  /** A partial update: only the fields sent change (SCRUM-142 calendar shares). */
  patch: <T>(path: string, body?: unknown, init?: RequestOptions) =>
    request<T>(path, { ...init, method: 'PATCH', body: body === undefined ? undefined : JSON.stringify(body) }),

  delete: <T>(path: string, init?: RequestOptions) => request<T>(path, { ...init, method: 'DELETE' }),

  /** A multipart upload — files go as they are, never base64 inside JSON. */
  postForm: <T>(path: string, form: FormData, init?: RequestOptions) =>
    request<T>(path, { ...init, method: 'POST', body: form }),

  /** Raw bytes as the body — one spoken sentence's audio (SCRUM-109) — typed by the Blob itself. */
  putBlob: <T>(path: string, body: Blob, init?: RequestOptions) =>
    request<T>(path, { ...init, method: 'PUT', body, headers: { 'Content-Type': body.type, ...init?.headers } }),

  /** A file's bytes; an error still carries its JSON body, so its `code` can be matched. */
  blob: (path: string, init?: RequestOptions) =>
    fetchOk(path, { ...init, method: 'GET', headers: { Accept: '*/*', ...init?.headers } }).then((res) => res.blob()),

  upload,
}
