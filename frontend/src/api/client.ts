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
  userCount?: number
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

export function configureAuth(hooks: AuthHooks) {
  auth = hooks
}

interface RequestOptions extends RequestInit {
  /** Send no bearer token and never try to refresh — for the sign-in endpoints themselves. */
  anonymous?: boolean
}

/**
 * Reads an error body if there is one. Several error responses are empty (`Content-Length: 0`),
 * and `res.json()` on an empty body throws, which would hide the real status behind a parse error.
 */
async function readErrorBody(res: Response): Promise<ApiErrorBody | undefined> {
  try {
    const text = await res.text()
    if (!text) return undefined
    const parsed = JSON.parse(text) as Partial<ApiErrorBody>
    return typeof parsed.code === 'string' ? (parsed as ApiErrorBody) : undefined
  } catch {
    return undefined
  }
}

async function send(path: string, init: RequestOptions, token: string | null) {
  const { anonymous: _anonymous, ...rest } = init
  return fetch(path, {
    ...rest,
    headers: {
      Accept: 'application/json',
      ...(rest.body ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...rest.headers,
    },
  })
}

async function request<T>(path: string, init: RequestOptions): Promise<T> {
  const authenticated = !init.anonymous && auth !== null
  let res = await send(path, init, authenticated ? await auth!.getAccessToken() : null)

  // An expired or revoked access token: refresh once and retry once. A second 401 means the
  // session is really gone, and `recoverFromUnauthorized` has already signed the user out.
  if (res.status === 401 && authenticated) {
    const token = await auth!.recoverFromUnauthorized()
    if (token) res = await send(path, init, token)
  }

  if (!res.ok) {
    const body = await readErrorBody(res)
    throw new ApiError(
      res.status,
      path,
      body?.message ?? `${init.method ?? 'GET'} ${path} → HTTP ${res.status}`,
      body,
    )
  }

  // 204 No Content has no body to parse.
  if (res.status === 204) return undefined as T

  return (await res.json()) as T
}

export const api = {
  get: <T>(path: string, init?: RequestOptions) => request<T>(path, { ...init, method: 'GET' }),

  post: <T>(path: string, body?: unknown, init?: RequestOptions) =>
    request<T>(path, { ...init, method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),

  put: <T>(path: string, body?: unknown, init?: RequestOptions) =>
    request<T>(path, { ...init, method: 'PUT', body: body === undefined ? undefined : JSON.stringify(body) }),

  delete: <T>(path: string, init?: RequestOptions) => request<T>(path, { ...init, method: 'DELETE' }),
}
