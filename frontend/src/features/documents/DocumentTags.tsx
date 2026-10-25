import { Icon } from '../../components/Icon'

/** The file-type tag in front of a name: violet PDF, blue DOCX (red is kept for "Mật" and warnings). */
export function FileTypeTag({ pdf }: { pdf: boolean }) {
  return <span className={pdf ? 'file-tag file-tag-pdf' : 'file-tag file-tag-docx'}>{pdf ? 'PDF' : 'DOCX'}</span>
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
