/**
 * TrustCabinet's mark: a gold star inside a state-blue ring.
 *
 * Drawn here rather than shipped as an asset so it scales cleanly and picks up the theme colours.
 */
export function BrandMark({ size = 32 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 40 40" aria-hidden="true">
      <circle cx="20" cy="20" r="18.5" fill="var(--surface)" stroke="var(--brand)" strokeWidth="2" />
      <path
        d="M20 9.5l3.2 6.6 7.3 1-5.3 5.1 1.3 7.2-6.5-3.4-6.5 3.4 1.3-7.2-5.3-5.1 7.3-1z"
        fill="var(--gold)"
      />
    </svg>
  )
}
