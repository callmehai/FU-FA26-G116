import type { LibraryDocument, LibraryFolder, LibraryUploadResponse, LibraryView } from '../types/document'
import { api } from './client'

/**
 * The personal library (SCRUM-110, SCRUM-111). Contract in docs/03-api/14-kho-tai-lieu-ca-nhan.md.
 *
 * Only the owner reads or changes it: someone else's folder or document answers 404 MSG175, a
 * destination folder that is gone 404 MSG179 — both mean "re-read the library".
 */

type Saved<K extends string, T> = { code: string; message: string } & Record<K, T>
type Done = { code: string; message: string }

/** UC-34: every folder, and the documents at `folderId` (root when null). `q` searches that place only. */
export function getLibrary(folderId: string | null, q = '', type: '' | 'pdf' | 'docx' = '') {
  const params = new URLSearchParams()
  if (folderId) params.set('folderId', folderId)
  if (q) params.set('q', q)
  if (type) params.set('type', type)
  const query = params.toString()
  return api.get<LibraryView>(`/api/documents${query ? `?${query}` : ''}`)
}

/** UC-36: all files or none; 409 MSG346 when the system's storage is full (ADR-0034), 400 MSG288/MSG164 as for a meeting. */
export function uploadToLibrary(
  folderId: string | null,
  files: { file: File; pageCount: number | null }[],
  options: { onProgress?: (loaded: number, total: number) => void; signal?: AbortSignal } = {},
) {
  const form = new FormData()
  for (const { file, pageCount } of files) {
    form.append('files', file)
    form.append('pageCounts', pageCount === null ? '' : String(pageCount))
  }
  return api.upload<LibraryUploadResponse>(
    `/api/documents/upload${folderId ? `?folderId=${encodeURIComponent(folderId)}` : ''}`,
    form,
    options,
  )
}

/** The original bytes, always as an attachment: fetch the Blob, never open the URL. */
export const getLibraryContent = (documentId: string, download = false) =>
  api.blob(`/api/documents/${documentId}/content${download ? '?download=true' : ''}`)

/** UC-71b: the extension must stay (400 MSG347). The copy already in a meeting keeps its name (BR-276). */
export const renameLibraryDocument = (documentId: string, fileName: string) =>
  api.put<Saved<'document', LibraryDocument>>(`/api/documents/${documentId}/name`, { fileName })

/** UC-71c: `null` moves it to the root. */
export const moveLibraryDocument = (documentId: string, folderId: string | null) =>
  api.put<Saved<'document', LibraryDocument>>(`/api/documents/${documentId}/folder`, { folderId })

/** UC-71a: the library row only; meetings using the file keep reading it (BR-186). */
export const deleteLibraryDocument = (documentId: string) => api.delete<Done>(`/api/documents/${documentId}`)

/** UC-72a: names may repeat (BR-221); five levels at most (409 MSG189). */
export const createFolder = (name: string, parentId: string | null) =>
  api.post<Saved<'folder', LibraryFolder>>('/api/folders', { name, parentId })

/** UC-72b */
export const renameFolder = (folderId: string, name: string) =>
  api.put<Saved<'folder', LibraryFolder>>(`/api/folders/${folderId}/name`, { name })

/** UC-72c: not into itself or below it (409 MSG193); the whole subtree must stay within five levels. */
export const moveFolder = (folderId: string, parentId: string | null) =>
  api.put<Saved<'folder', LibraryFolder>>(`/api/folders/${folderId}/parent`, { parentId })

/** UC-72d: only an empty folder (409 MSG196). */
export const deleteFolder = (folderId: string) => api.delete<Done>(`/api/folders/${folderId}`)
