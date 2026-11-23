// The report query's parameters (supervisor review 22/11/2026): which main table, how to group, sort and cut it, and one
// filter per column with a fixed list of values. Mirrors the server's ReportCatalog and ReportLabels; the server checks
// every value again (MSG484). Imports nothing, so `npm test` runs it with node alone.

export type Entity = 'meetings' | 'participants' | 'votes' | 'documents' | 'minutes' | 'recordings'
export type Format = 'count' | 'percent' | 'hours' | 'megabytes'

export interface EntityConfig {
  key: Entity
  label: string
  /** The groupings offered, the default first. */
  groups: string[]
  /** The figures the server computes, in its order; the first is the default sort. */
  measures: string[]
  /** The filters offered besides the meeting's own (status, mode, sensitivity). */
  filters: string[]
  /** The Excel file's name: bao-cao-{slug}_{from}_{to}.xlsx, as the server names it. */
  slug: string
}

/** Grouped by these, every table can also be grouped by month, by meeting and by the meeting's own columns. */
const COMMON_GROUPS = ['month', 'meeting', 'status', 'mode', 'sensitivity']
const COMMON_FILTERS = ['status', 'mode', 'sensitivity']

const groups = (...own: string[]) => [...own, ...COMMON_GROUPS.filter((g) => !own.includes(g))]

export const ENTITIES: EntityConfig[] = [
  {
    key: 'meetings',
    label: 'Cuộc họp',
    groups: groups('month', 'chair', 'creator'),
    measures: ['count', 'ended', 'cancelled', 'hours', 'attendance'],
    filters: [],
    slug: 'cuoc-hop',
  },
  {
    key: 'participants',
    label: 'Người dự họp',
    groups: groups('person', 'role', 'attendance', 'response'),
    measures: ['count', 'present', 'absent', 'rate'],
    filters: ['role', 'attendance'],
    slug: 'thanh-phan',
  },
  {
    key: 'votes',
    label: 'Biểu quyết',
    groups: groups('conclusion', 'voteStatus', 'ballot'),
    measures: ['count', 'passed', 'notPassed', 'turnout'],
    filters: ['conclusion'],
    slug: 'bieu-quyet',
  },
  {
    key: 'documents',
    label: 'Tài liệu',
    groups: groups('uploader', 'fileType'),
    measures: ['count', 'pages', 'size', 'comments', 'reads'],
    filters: ['fileType'],
    slug: 'tai-lieu',
  },
  {
    key: 'minutes',
    label: 'Biên bản',
    groups: groups('minutesStatus', 'author'),
    measures: ['count', 'issued', 'waiting', 'returned', 'turnaround'],
    filters: ['minutesStatus'],
    slug: 'bien-ban',
  },
  {
    key: 'recordings',
    label: 'Bản ghi',
    groups: groups('month', 'source', 'transcript'),
    measures: ['count', 'hours', 'transcribed'],
    filters: ['transcript'],
    slug: 'ban-ghi',
  },
]

const BY_KEY = new Map(ENTITIES.map((e) => [e.key, e]))

export const GROUP_LABEL: Record<string, string> = {
  month: 'Tháng',
  meeting: 'Từng cuộc họp',
  status: 'Trạng thái cuộc họp',
  mode: 'Hình thức',
  sensitivity: 'Mức độ',
  chair: 'Chủ toạ',
  creator: 'Người tạo',
  person: 'Người dự',
  role: 'Vai trò',
  attendance: 'Điểm danh',
  response: 'Trả lời mời',
  conclusion: 'Kết luận',
  voteStatus: 'Trạng thái biểu quyết',
  ballot: 'Hình thức bỏ phiếu',
  uploader: 'Người đưa lên',
  fileType: 'Loại tệp',
  minutesStatus: 'Trạng thái biên bản',
  author: 'Người lập',
  source: 'Nguồn ghi',
  transcript: 'Gỡ băng',
}

export interface FilterConfig {
  /** The field's name. */
  label: string
  /** The option that filters nothing. */
  all: string
  values: [string, string][]
}

export const FILTERS: Record<string, FilterConfig> = {
  status: {
    label: 'Trạng thái cuộc họp',
    all: 'Mọi trạng thái',
    values: [
      ['Draft', 'Nháp'],
      ['WaitingApproval', 'Chờ duyệt'],
      ['Rejected', 'Bị từ chối'],
      ['Approved', 'Đã duyệt'],
      ['Published', 'Đã công bố'],
      ['InProgress', 'Đang diễn ra'],
      ['Ended', 'Đã kết thúc'],
      ['Cancelled', 'Đã huỷ'],
    ],
  },
  mode: {
    label: 'Hình thức',
    all: 'Mọi hình thức',
    values: [
      ['InPerson', 'Trực tiếp'],
      ['Online', 'Trực tuyến'],
      ['Hybrid', 'Kết hợp'],
    ],
  },
  sensitivity: {
    label: 'Mức độ',
    all: 'Mọi mức độ',
    values: [
      ['Normal', 'Thường'],
      ['Internal', 'Nội bộ'],
      ['Secret', 'Mật'],
    ],
  },
  role: {
    label: 'Vai trò',
    all: 'Mọi vai trò',
    values: [
      ['Chair', 'Chủ toạ'],
      ['Secretary', 'Thư ký'],
      ['Member', 'Thành viên'],
      ['Guest', 'Khách'],
    ],
  },
  attendance: {
    label: 'Điểm danh',
    all: 'Mọi điểm danh',
    values: [
      ['Present', 'Có mặt'],
      ['Absent', 'Vắng'],
      ['Excused', 'Vắng có phép'],
      ['Delegated', 'Ủy quyền'],
      ['Unknown', 'Chưa điểm danh'],
    ],
  },
  conclusion: {
    label: 'Kết luận',
    all: 'Mọi kết luận',
    values: [
      ['Passed', 'Thông qua'],
      ['NotPassed', 'Không thông qua'],
      ['Deferred', 'Hoãn'],
      ['None', 'Chưa kết luận'],
    ],
  },
  fileType: {
    label: 'Loại tệp',
    all: 'Mọi loại tệp',
    values: [
      ['pdf', 'PDF'],
      ['word', 'Word'],
      ['excel', 'Excel'],
      ['powerpoint', 'PowerPoint'],
      ['image', 'Ảnh'],
      ['other', 'Khác'],
    ],
  },
  minutesStatus: {
    label: 'Trạng thái biên bản',
    all: 'Mọi trạng thái',
    values: [
      ['Draft', 'Nháp'],
      ['WaitingApproval', 'Chờ duyệt'],
      ['Rejected', 'Bị trả lại'],
      ['Issued', 'Đã ban hành'],
      ['Superseded', 'Đã đính chính'],
    ],
  },
  transcript: {
    label: 'Gỡ băng',
    all: 'Mọi trạng thái gỡ băng',
    values: [
      ['Completed', 'Đã gỡ băng'],
      ['Processing', 'Đang gỡ băng'],
      ['Pending', 'Chờ gỡ băng'],
      ['Failed', 'Lỗi gỡ băng'],
      ['None', 'Chưa gỡ băng'],
    ],
  },
}

/** Groupings by person or by meeting have many groups: they open on the top ten. */
const LONG_GROUPS = new Set(['meeting', 'chair', 'creator', 'person', 'uploader', 'author'])
export const TOP_VALUES = ['5', '10', '20', '50'] as const
/** The sort that keeps the groups in their own order: months in time, states as listed, names alphabetically. */
export const GROUP_SORT = 'group'

export interface ReportQuery {
  entity: Entity
  groupBy: string
  sort: string
  /** Empty: every group. */
  top: string
  /** Only the filters this table has, each with a known value. */
  filters: Record<string, string>
}

export const entityConfig = (entity: Entity) => BY_KEY.get(entity)!

/** Every filter of a table: the meeting's own, then the table's. */
export const filtersOf = (entity: Entity) => [...COMMON_FILTERS, ...entityConfig(entity).filters]

/** The default sort and top of a grouping: time order for months, otherwise biggest first; long lists cut to ten. */
export function defaultsFor(entity: Entity, groupBy: string) {
  return {
    // One meeting per row counts 1 each: the longest meetings come first instead.
    sort:
      groupBy === 'month'
        ? GROUP_SORT
        : entity === 'meetings' && groupBy === 'meeting'
          ? 'hours'
          : entityConfig(entity).measures[0],
    top: LONG_GROUPS.has(groupBy) ? '10' : '',
  }
}

/**
 * The query the URL holds. Anything this table does not have — an unknown table, grouping, sort, top or filter value —
 * is dropped, as an unknown filter value is on the meeting list, and the default takes its place.
 */
export function parseQuery(get: (name: string) => string | null): ReportQuery {
  const entity = BY_KEY.has(get('entity') as Entity) ? (get('entity') as Entity) : 'meetings'
  const config = entityConfig(entity)
  const groupBy = config.groups.includes(get('groupBy') ?? '') ? get('groupBy')! : config.groups[0]
  const defaults = defaultsFor(entity, groupBy)
  const sortParam = get('sort') ?? ''
  const sort = sortParam === GROUP_SORT || config.measures.includes(sortParam) ? sortParam : defaults.sort
  const topParam = get('top')
  const top =
    topParam === null
      ? defaults.top
      : topParam === 'all' || topParam === ''
        ? ''
        : TOP_VALUES.includes(topParam as never)
          ? topParam
          : defaults.top
  const filters: Record<string, string> = {}
  for (const key of filtersOf(entity)) {
    const value = get(key)
    if (value && FILTERS[key].values.some(([v]) => v === value)) filters[key] = value
  }
  return { entity, groupBy, sort, top, filters }
}

/** The query as the URL keeps it: only what differs from the defaults, so a plain link stays short. */
export function queryToUrl(query: ReportQuery): Record<string, string> {
  const defaults = defaultsFor(query.entity, query.groupBy)
  const out: Record<string, string> = {}
  if (query.entity !== 'meetings') out.entity = query.entity
  if (query.groupBy !== entityConfig(query.entity).groups[0]) out.groupBy = query.groupBy
  if (query.sort !== defaults.sort) out.sort = query.sort
  if (query.top !== defaults.top) out.top = query.top || 'all'
  return { ...out, ...query.filters }
}

/** The query as the server takes it, with the period. */
export function queryToApi(query: ReportQuery, from: string, to: string): Record<string, string> {
  const out: Record<string, string> = { entity: query.entity, from, to, groupBy: query.groupBy, sort: query.sort }
  if (query.top) out.top = query.top
  return { ...out, ...query.filters }
}

/** Another table: its default grouping, no filters but the meeting's own that it keeps. */
export function switchEntity(query: ReportQuery, entity: Entity): ReportQuery {
  const groupBy = entityConfig(entity).groups[0]
  const kept = Object.fromEntries(Object.entries(query.filters).filter(([key]) => COMMON_FILTERS.includes(key)))
  return { entity, groupBy, ...defaultsFor(entity, groupBy), filters: kept }
}

/** Another grouping, with its default sort and top. */
export const switchGroup = (query: ReportQuery, groupBy: string): ReportQuery => ({
  ...query,
  groupBy,
  ...defaultsFor(query.entity, groupBy),
})

export const exportFileName = (entity: Entity, from: string, to: string) =>
  `bao-cao-${entityConfig(entity).slug}_${from}_${to}.xlsx`

const integer = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 })
const oneDecimal = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 })
const percent = new Intl.NumberFormat('vi-VN', { style: 'percent', maximumFractionDigits: 1 })

/** A figure as people read it: 1.394, 74,3 %, 24,6. Null when there is nothing to show. */
export function formatFigure(value: number | null | undefined, format: Format): string | null {
  if (value === null || value === undefined) return null
  switch (format) {
    case 'percent':
      return percent.format(value)
    case 'hours':
    case 'megabytes':
      return oneDecimal.format(value)
    default:
      return integer.format(value)
  }
}
