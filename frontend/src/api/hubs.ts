import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { getAccessToken } from './client'

/**
 * Realtime hubs (SignalR). Same origin as the REST API: in development Vite proxies `/hubs` to the
 * backend with WebSockets enabled, in production the backend serves both.
 *
 * Browsers cannot put an Authorization header on a WebSocket, so the token goes in the query
 * string; the backend accepts it there only on hub paths. It is asked for on every (re)connect, so
 * a reconnect after the server closed an expired connection carries a freshly refreshed token.
 */

/** Quick first retries, then every 30 s for as long as the page stays open. */
const RETRY_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000]

export function retryDelay(attempt: number) {
  return RETRY_DELAYS_MS[Math.min(attempt, RETRY_DELAYS_MS.length - 1)]
}

export function createHubConnection(path: string): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(path, { accessTokenFactory: async () => (await getAccessToken()) ?? '' })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: ({ previousRetryCount }) => retryDelay(previousRetryCount),
    })
    .configureLogging(LogLevel.Warning)
    .build()
}
