// The report query's URL handling (supervisor review 22/11/2026): what the URL holds, what it drops, what reaches the server.
// Run with `npm test` (node --test, which strips the types itself); reportQuery.ts imports nothing for that reason.
import assert from 'node:assert/strict'
import { test } from 'node:test'
import {
  ENTITIES,
  FILTERS,
  GROUP_LABEL,
  exportFileName,
  formatFigure,
  parseQuery,
  queryToApi,
  queryToUrl,
  switchEntity,
  switchGroup,
} from '../../src/features/reports/reportQuery.ts'

const from = (search: string) => {
  const params = new URLSearchParams(search)
  return parseQuery((name) => params.get(name))
}

test('an empty URL reads meetings by month in time order, every group', () => {
  assert.deepEqual(from(''), { entity: 'meetings', groupBy: 'month', sort: 'group', top: '', filters: {} })
})

test('a grouping by person opens on the top ten, biggest first', () => {
  assert.deepEqual(from('entity=participants'), {
    entity: 'participants',
    groupBy: 'person',
    sort: 'count',
    top: '10',
    filters: {},
  })
})

test('anything the table does not have is dropped for its default', () => {
  const query = from('entity=votes&groupBy=person&sort=hours&top=7&status=Weird&attendance=Present&conclusion=Passed')
  assert.deepEqual(query, {
    entity: 'votes',
    groupBy: 'conclusion',
    sort: 'count',
    top: '',
    filters: { conclusion: 'Passed' },
  })
  assert.equal(from('entity=nope').entity, 'meetings')
})

test('the URL keeps only what differs from the defaults, and reads back the same', () => {
  const query = {
    entity: 'votes' as const,
    groupBy: 'conclusion',
    sort: 'passed',
    top: '5',
    filters: { mode: 'Online' },
  }
  const url = queryToUrl(query)
  assert.deepEqual(url, { entity: 'votes', sort: 'passed', top: '5', mode: 'Online' })
  assert.deepEqual(from(new URLSearchParams(url).toString()), query)
  // "Every group" where the default is ten is kept as top=all.
  const all = { ...from('entity=participants'), top: '' }
  assert.equal(queryToUrl(all).top, 'all')
  assert.equal(from('entity=participants&top=all').top, '')
})

test('the server gets the period, the table, its grouping, sort, top and filters', () => {
  assert.deepEqual(queryToApi(from('entity=documents&fileType=pdf&top=5'), '2026-09', '2026-11'), {
    entity: 'documents',
    from: '2026-09',
    to: '2026-11',
    groupBy: 'uploader',
    sort: 'count',
    top: '5',
    fileType: 'pdf',
  })
})

test('switching table keeps the meeting filters only; switching grouping resets sort and top', () => {
  const query = from('entity=participants&attendance=Absent&sensitivity=Secret&sort=absent&top=5')
  assert.deepEqual(switchEntity(query, 'minutes'), {
    entity: 'minutes',
    groupBy: 'minutesStatus',
    sort: 'count',
    top: '',
    filters: { sensitivity: 'Secret' },
  })
  assert.deepEqual(switchGroup(query, 'month'), { ...query, groupBy: 'month', sort: 'group', top: '' })
  assert.equal(switchGroup(from(''), 'meeting').sort, 'hours')
})

test('every grouping and filter offered has words, and the file is named as the server names it', () => {
  for (const entity of ENTITIES) {
    for (const group of entity.groups) assert.ok(GROUP_LABEL[group], group)
    for (const filter of entity.filters) assert.ok(FILTERS[filter], filter)
  }
  assert.equal(exportFileName('votes', '2026-09', '2026-11'), 'bao-cao-bieu-quyet_2026-09_2026-11.xlsx')
})

test('figures read the Vietnamese way, and nothing stays nothing', () => {
  assert.equal(formatFigure(1394, 'count'), '1.394')
  assert.equal(formatFigure(24.56, 'hours'), '24,6')
  assert.match(formatFigure(0.7428, 'percent')!, /^74,3\s?%$/)
  assert.equal(formatFigure(null, 'percent'), null)
})
