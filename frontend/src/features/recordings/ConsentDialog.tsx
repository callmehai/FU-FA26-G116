import { useState } from 'react'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { formatSize } from './format'

/**
 * SCRUM-29 — nothing is recorded or kept before the Secretary confirms the participants were told and nobody objected
 * (Law 91/2025, art. 32: a meeting may be recorded, but the people in it must be told). The purpose they confirm is
 * stored with the file.
 */

export type Consent = { kind: 'record' } | { kind: 'upload'; file: File }

export function ConsentDialog({
  consent,
  purpose,
  onPurpose,
  onClose,
  onConfirm,
}: {
  consent: Consent
  purpose: string
  onPurpose: (value: string) => void
  onClose: () => void
  onConfirm: () => void
}) {
  const [confirmed, setConfirmed] = useState(false)
  const record = consent.kind === 'record'
  return (
    <Dialog
      title={record ? 'Bắt đầu ghi âm' : 'Lưu tệp ghi âm'}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn-ghost" onClick={onClose}>
            Huỷ
          </button>
          <button type="button" className="btn btn-primary" disabled={!confirmed} onClick={onConfirm}>
            <Icon name={record ? 'mic' : 'upload'} size={18} />
            {record ? 'Bắt đầu ghi' : 'Tải lên'}
          </button>
        </>
      }
    >
      {consent.kind === 'upload' && (
        <p className="consent-file">
          <Icon name="fileAudio" size={16} /> {consent.file.name} · {formatSize(consent.file.size)}
        </p>
      )}
      <label className="field">
        <span>Mục đích ghi âm</span>
        <input value={purpose} maxLength={1000} onChange={(e) => onPurpose(e.target.value)} />
      </label>
      <label className="consent-check">
        <input type="checkbox" checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} />
        <span>Tôi đã thông báo cho người dự rằng cuộc họp được ghi âm cho mục đích trên, và không ai phản đối.</span>
      </label>
      <p className="field-hint">Chỉ người trong cuộc họp được nghe; bản ghi tự xoá khi hết thời hạn lưu trữ.</p>
    </Dialog>
  )
}
