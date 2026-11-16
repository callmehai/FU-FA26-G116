import { useEffect, useState } from 'react'

const PHONE = '(max-width: 640px)'

/** Whether the screen is a phone's, following rotation and resizing. */
export function usePhone() {
  return useMedia(PHONE)
}

/** Whether a media query matches, following resizing. */
export function useMedia(media: string) {
  const [matches, setMatches] = useState(() => window.matchMedia(media).matches)
  useEffect(() => {
    const query = window.matchMedia(media)
    const follow = () => setMatches(query.matches)
    follow()
    query.addEventListener('change', follow)
    return () => query.removeEventListener('change', follow)
  }, [media])
  return matches
}
