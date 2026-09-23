import { ApiError } from '../api/client'

/**
 * Turns any failure into one Vietnamese sentence a user can act on.
 *
 * The backend's own `message` wins when there is one: it is already written for the user and
 * specific to the case. Branching on what went wrong must use `ApiError.code`, never this text.
 */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.body?.message) return error.body.message
    if (error.status >= 500) return 'Máy chủ đang gặp sự cố. Vui lòng thử lại sau.'
    return `Yêu cầu không thành công (HTTP ${error.status}).`
  }
  // fetch rejects with a TypeError when the request never reached the server.
  if (error instanceof TypeError) return 'Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại.'
  if (error instanceof Error && error.message) return error.message
  return 'Đã có lỗi xảy ra. Vui lòng thử lại.'
}

export function isApiError(error: unknown, status: number, code?: string): error is ApiError {
  return error instanceof ApiError && error.status === status && (code === undefined || error.code === code)
}
