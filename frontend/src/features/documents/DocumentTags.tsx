import { Icon } from '../../components/Icon'

/**
 * The file-type icon in front of a name: a page with a folded corner and a band naming the type, violet for
 * PDF and blue for DOCX (red is kept for "Mật" and warnings). The band is drawn, so the type is also its label.
 */
export function FileTypeTag({ pdf, size = 32 }: { pdf: boolean; size?: number }) {
  const label = pdf ? 'PDF' : 'DOCX'
  return (
    <svg
      className={pdf ? 'file-icon file-icon-pdf' : 'file-icon file-icon-docx'}
      viewBox="0 0 24 28"
      width={(size * 24) / 28}
      height={size}
      role="img"
      aria-label={label}
    >
      <path
        className="file-icon-page"
        d="M5.5 .75h10l6.75 6.75v17.75a2 2 0 0 1-2 2H5.5a2 2 0 0 1-2-2V2.75a2 2 0 0 1 2-2Z"
      />
      <path className="file-icon-fold" d="M15.5 .75V5.5a2 2 0 0 0 2 2h4.75" />
      <rect className="file-icon-band" x=".5" y="13.5" width={pdf ? 18 : 21.5} height="9.5" rx="2" />
      <text className="file-icon-text" x={pdf ? 9.5 : 11.25} y="20.75" textAnchor="middle" fontSize={pdf ? 7 : 6.25}>
        {label}
      </text>
    </svg>
  )
}

/** BR-188: a Secret meeting's documents are read in the browser only. */
export function OnlineOnly() {
  return (
    <span className="online-only" data-tooltip="Cuộc họp Mật: tài liệu chỉ xem trực tuyến, không tải về">
      <Icon name="lock" size={14} />
      Chỉ xem trực tuyến
    </span>
  )
}
