import { PDF_TYPE, type MeetingDocument } from '../../types/document'

const size = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 })

/** "640 KB", "1,6 MB", "1 GB" — the way the upload limits and the storage limit are written. */
export function formatBytes(bytes: number) {
  if (bytes < 1024 * 1024) return `${size.format(Math.max(1, Math.round(bytes / 1024)))} KB`
  if (bytes < 1024 * 1024 * 1024) return `${size.format(bytes / (1024 * 1024))} MB`
  return `${size.format(bytes / (1024 * 1024 * 1024))} GB`
}

export const isPdf = (doc: Pick<MeetingDocument, 'contentType'>) => doc.contentType === PDF_TYPE

/** The line under a name: page count (PDF only, when known) and comments, when there are any. */
export function documentFacts(doc: MeetingDocument) {
  const facts = []
  if (doc.pageCount) facts.push(`${doc.pageCount} trang`)
  if (doc.commentCount > 0) facts.push(`${doc.commentCount} góp ý`)
  return facts.join(', ')
}

/** Hands a fetched file to the browser as a download under its original name. */
export function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.append(link)
  link.click()
  link.remove()
  // The click starts the download synchronously; the URL only has to outlive this task.
  setTimeout(() => URL.revokeObjectURL(url))
}
