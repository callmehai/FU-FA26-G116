/** Initials and a stable tint for a person's avatar — the sidebar account and the user list. */

/**
 * "Nguyễn Văn An" → "NA": the family name's initial, then the given name's. Words that do not start
 * with a letter ("Nguyễn Thư Ký 2") are skipped, so a number never ends up in the avatar.
 */
export function initials(name: string) {
  const words = name
    .trim()
    .split(/\s+/)
    .filter((w) => /^\p{L}/u.test(w))
  if (words.length === 0) return '?'
  const first = words[0].charAt(0)
  const last = words.length > 1 ? words[words.length - 1].charAt(0) : ''
  return (first + last).toUpperCase()
}

/** Soft hues, kept off the brand blue so an avatar never reads as a primary button. */
const AVATAR_HUES = [28, 45, 95, 150, 190, 255, 290, 330]

/** A stable tint per name: same person, same colour, on every screen and every visit. */
export function avatarColors(name: string) {
  let hash = 0
  for (const char of name) hash = (hash * 31 + char.charCodeAt(0)) >>> 0
  const hue = AVATAR_HUES[hash % AVATAR_HUES.length]
  return { bg: `hsl(${hue} 62% 88%)`, fg: `hsl(${hue} 48% 28%)` }
}
