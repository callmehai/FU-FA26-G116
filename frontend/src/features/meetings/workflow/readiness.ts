/**
 * What still blocks a submission, one sentence per problem. The backend keys its readiness errors by
 * field, one key per agenda item (`agendaItems[<id>].title`), so the same sentence can come back
 * several times; it is listed once.
 */
export function readinessProblems(errors: unknown): string[] {
  if (!errors || typeof errors !== 'object') return []
  return [...new Set(Object.values(errors).filter((value): value is string => typeof value === 'string'))]
}
