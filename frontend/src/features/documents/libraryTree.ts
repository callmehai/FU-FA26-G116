import type { LibraryFolder } from '../../types/document'

/** The library root's name wherever a place is named: the trail, the move dialog, the upload target. */
export const LIBRARY_ROOT = 'Kho tài liệu'

/** A folder's sub-folders, by name. */
export function childrenOf(folders: LibraryFolder[], parentId: string | null) {
  return folders.filter((f) => f.parentId === parentId).sort((a, b) => a.name.localeCompare(b.name, 'vi'))
}

/** The folders from the root down to `folderId`, root excluded; empty at the root or for an unknown id. */
export function trailOf(folders: LibraryFolder[], folderId: string | null) {
  const byId = new Map(folders.map((f) => [f.id, f]))
  const trail: LibraryFolder[] = []
  for (let id = folderId; id;) {
    const folder = byId.get(id)
    if (!folder || trail.includes(folder)) break
    trail.unshift(folder)
    id = folder.parentId
  }
  return trail
}

/** `folderId` and every folder below it: where a folder may not be moved (BR-222). */
export function subtreeOf(folders: LibraryFolder[], folderId: string) {
  const ids = new Set([folderId])
  for (let grew = true; grew;) {
    grew = false
    for (const f of folders)
      if (f.parentId && ids.has(f.parentId) && !ids.has(f.id)) {
        ids.add(f.id)
        grew = true
      }
  }
  return ids
}

export const placeName = (folders: LibraryFolder[], folderId: string | null) =>
  folders.find((f) => f.id === folderId)?.name ?? LIBRARY_ROOT
