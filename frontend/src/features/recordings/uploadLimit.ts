/**
 * The recording upload's size limit (BR-340–BR-357, `Recordings:MaxUploadMegabytes`), as the server's list gives it
 * (`maxUploadBytes`). The server refuses a bigger file with 413 MSG293 while the browser is still sending it and drops
 * the connection, so the browser only sees a network error: the size is checked here before anything is sent, and a
 * dropped connection while sending a file over the limit is read as that same refusal.
 *
 * Imports nothing, so `npm test` (node --test) runs it as it is.
 */

const MB = 1024 * 1024

/** `maxBytes` 0 or less: the limit is not known (an older server), and nothing is refused here. */
export function isTooLarge(size: number, maxBytes: number | undefined): boolean {
  return maxBytes !== undefined && maxBytes > 0 && size > maxBytes
}

/** MSG293's wording, with the server's own limit. */
export function tooLargeMessage(maxBytes: number): string {
  return `Bản ghi vượt quá ${Math.floor(maxBytes / MB)} MB.`
}

/** "tối đa 300 MB", for the upload button's hint. */
export function limitHint(maxBytes: number | undefined): string | null {
  return maxBytes !== undefined && maxBytes > 0 ? `tối đa ${Math.floor(maxBytes / MB)} MB` : null
}

/** The failure of a recording upload as the person should read it. */
export function explainUploadFailure(error: unknown, size: number, maxBytes: number | undefined): unknown {
  // fetch and XMLHttpRequest report a dropped connection as a TypeError (api/client.ts).
  if (error instanceof TypeError && isTooLarge(size, maxBytes)) return new Error(tooLargeMessage(maxBytes!))
  return error
}
