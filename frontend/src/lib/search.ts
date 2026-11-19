/**
 * Every search box matches without Vietnamese marks or case (Khánh 19/11/2026): "hop thuong" finds "Họp thường",
 * "nguyen" finds "Nguyễn", "dong" finds "Đông". The backend folds the same way (PostgreSQL `unaccent` + `lower`, and
 * `SearchText.Fold` for lists it filters in memory), so a box filtered here and one sent as `q` agree.
 */
export function normalizeSearch(text: string): string {
  return text.normalize('NFD').replace(/\p{M}/gu, '').replace(/[đĐ]/g, 'd').toLowerCase()
}

/** Whether `text` contains `query`, both folded; an empty query matches everything. */
export function matchesSearch(text: string | null | undefined, query: string): boolean {
  const needle = normalizeSearch(query.trim())
  return needle === '' || (text != null && normalizeSearch(text).includes(needle))
}
