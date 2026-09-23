/**
 * No 0/O, 1/l/I: an administrator reads this password out or retypes it for someone, and those
 * pairs are where that goes wrong. 56 symbols × 16 characters is about 93 bits — length is what the
 * backend asks for (12 minimum), not character classes.
 */
const ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789'
const DEFAULT_LENGTH = 16

/**
 * A random password from the browser's CSPRNG. Bytes that would bias the result towards the start
 * of the alphabet (256 is not a multiple of 56) are thrown away rather than folded in with `%`.
 */
export function generatePassword(length = DEFAULT_LENGTH) {
  const limit = 256 - (256 % ALPHABET.length)
  const chars: string[] = []
  const bytes = new Uint8Array(length * 2)

  while (chars.length < length) {
    crypto.getRandomValues(bytes)
    for (const byte of bytes) {
      if (byte < limit) chars.push(ALPHABET[byte % ALPHABET.length])
      if (chars.length === length) break
    }
  }
  return chars.join('')
}
