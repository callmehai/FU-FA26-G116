import { useEffect, useRef, useState } from 'react'

/**
 * The OAuth client the backend verifies ID tokens against (`Authentication:Google:ClientId` in
 * appsettings.json). Not a secret — the browser sends it to Google in the clear. It has to match
 * the backend's value exactly, or every token comes back MSG03 for the wrong audience.
 *
 * ponytail: duplicated from appsettings.json because the backend has no endpoint exposing it.
 * Ask BE for one if the value ever differs between environments.
 */
const GOOGLE_CLIENT_ID = '1024662324975-ma0lj2lpl48kdl3bb56aj53nk3hsnd5f.apps.googleusercontent.com'

const GIS_SRC = 'https://accounts.google.com/gsi/client'

interface GoogleCredentialResponse {
  /** The ID token — a JWT. This is what the backend wants, not an OAuth access token. */
  credential?: string
}

interface GoogleAccountsId {
  initialize: (config: { client_id: string; callback: (response: GoogleCredentialResponse) => void }) => void
  renderButton: (parent: HTMLElement, options: Record<string, string | number>) => void
}

declare global {
  interface Window {
    google?: { accounts: { id: GoogleAccountsId } }
  }
}

let gisLoading: Promise<GoogleAccountsId> | null = null

/** Loads Google Identity Services once, on the first visit to the login page. */
function loadGoogleIdentityServices() {
  gisLoading ??= new Promise<GoogleAccountsId>((resolve, reject) => {
    if (window.google?.accounts?.id) return resolve(window.google.accounts.id)

    const script = document.createElement('script')
    script.src = GIS_SRC
    script.async = true
    script.onload = () =>
      window.google?.accounts?.id ? resolve(window.google.accounts.id) : reject(new Error('GIS missing'))
    script.onerror = () => reject(new Error('GIS failed to load'))
    document.head.append(script)
  }).catch((error: unknown) => {
    gisLoading = null // let the next visit try again
    throw error
  })
  return gisLoading
}

/**
 * UC-02's "Continue with Google". Google draws the button itself; this component only hosts it.
 *
 * The outer branch of the UC-02 diagram — Google failing or the user cancelling — stays here and
 * never reaches the backend: a cancelled popup simply produces no credential, and a Google failure
 * is reported through `onError`.
 */
export function GoogleSignInButton({
  onCredential,
  onError,
  disabled,
}: {
  onCredential: (idToken: string) => void
  onError: () => void
  disabled: boolean
}) {
  const hostRef = useRef<HTMLDivElement>(null)
  const [unavailable, setUnavailable] = useState(false)

  // Google keeps the first callback it is given; route it through refs so it always calls the
  // latest props.
  const onCredentialRef = useRef(onCredential)
  const onErrorRef = useRef(onError)
  useEffect(() => {
    onCredentialRef.current = onCredential
    onErrorRef.current = onError
  })

  useEffect(() => {
    let cancelled = false

    loadGoogleIdentityServices()
      .then((gis) => {
        const host = hostRef.current
        if (cancelled || !host) return

        gis.initialize({
          client_id: GOOGLE_CLIENT_ID,
          callback: (response) =>
            response.credential ? onCredentialRef.current(response.credential) : onErrorRef.current(),
        })

        gis.renderButton(host, {
          type: 'standard',
          theme: 'outline',
          size: 'large',
          text: 'continue_with',
          shape: 'rectangular',
          logo_alignment: 'center',
          locale: 'vi',
          // Google caps the button at 400px and needs a number, not "100%".
          width: Math.min(400, Math.max(200, host.offsetWidth)),
        })
      })
      .catch(() => {
        if (!cancelled) setUnavailable(true)
      })

    return () => {
      cancelled = true
    }
  }, [])

  if (unavailable) {
    return <p className="login-hint">Không tải được dịch vụ đăng nhập của Google. Vui lòng thử lại sau.</p>
  }

  return <div ref={hostRef} className={`google-button${disabled ? ' google-button-disabled' : ''}`} />
}
