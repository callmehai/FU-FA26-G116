import { Icon } from '../../components/Icon'
import { ZOOMS } from './zoom'

/** Zoom out / the level / zoom in; `zoom` indexes `ZOOMS`. Shared by the meeting's and the library's viewers. */
export function ViewerZoom({ zoom, onZoom }: { zoom: number; onZoom: (zoom: number) => void }) {
  return (
    <div className="viewer-zoom" role="group" aria-label="Thu phóng">
      <button
        type="button"
        className="icon-button icon-button-sm"
        aria-label="Thu nhỏ"
        data-tooltip="Thu nhỏ"
        disabled={zoom === 0}
        onClick={() => onZoom(zoom - 1)}
      >
        <Icon name="zoomOut" size={16} />
      </button>
      <span className="viewer-zoom-value">{ZOOMS[zoom] * 100}%</span>
      <button
        type="button"
        className="icon-button icon-button-sm"
        aria-label="Phóng to"
        data-tooltip="Phóng to"
        disabled={zoom === ZOOMS.length - 1}
        onClick={() => onZoom(zoom + 1)}
      >
        <Icon name="zoomIn" size={16} />
      </button>
    </div>
  )
}
