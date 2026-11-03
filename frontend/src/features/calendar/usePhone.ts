import { useEffect, useState } from 'react'

const PHONE = '(max-width: 640px)'

/** Whether the screen is a phone's, following rotation and resizing. */
export function usePhone() {
  const [phone, setPhone] = useState(() => window.matchMedia(PHONE).matches)
  useEffect(() => {
    const query = window.matchMedia(PHONE)
    const follow = () => setPhone(query.matches)
    query.addEventListener('change', follow)
    return () => query.removeEventListener('change', follow)
  }, [])
  return phone
}
