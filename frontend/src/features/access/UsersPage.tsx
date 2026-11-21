import { Fragment, useCallback, useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { listRoles } from "../../api/roles";
import { changeUserStatus, getUser, listUsers, requestPasswordReset } from "../../api/users";
import { Badge } from "../../components/Badge";
import { ConfirmDialog } from "../../components/ConfirmDialog";
import { Icon } from "../../components/Icon";
import { PageHeader } from "../../components/PageHeader";
import { Pagination } from "../../components/Pagination";
import { Select } from "../../components/Select";
import { useToast } from "../../components/toastContext";
import { errorMessage, isApiError } from "../../lib/errors";
import { avatarColors, initials } from "../../lib/avatar";
import { formatDate } from "../../lib/format";
import { routes } from "../../lib/routes";
import { tipIfClipped } from "../../lib/tipIfClipped";
import { useSearchBox } from "../../lib/useSearchBox";
import type { RoleRecord } from "../../types/role";
import type { UserAccount, UserMutationResponse } from "../../types/user";
import { useAccessLost } from "../auth/useAccessLost";
import { useAuth } from "../auth/useAuth";
import { CollectForMeeting } from "../voice/CollectForMeeting";
import { Dialog } from "../../components/Dialog";
import { UserFormDialog, type UserFormTarget } from "./UserFormDialog";
import { latestSearchParams } from "../../lib/latestSearchParams";
import { normalizeSearch } from "../../lib/search";
import { useRefetchOnFocus } from "../../lib/useRefetchOnFocus";

type RoleFilter = string | "all";
type StatusFilter = "all" | "active" | "inactive";
type FilterKey = "q" | "role" | "status" | "page" | "pageSize";

const STATUS_FILTER_OPTIONS: { value: StatusFilter; label: string }[] = [
  { value: "all", label: "Mọi trạng thái" },
  { value: "active", label: "Đang hoạt động" },
  { value: "inactive", label: "Đã vô hiệu hoá" },
];

const COLUMN_COUNT = 6;
const PAGE_SIZES = [20, 50, 100] as const;
const DEFAULT_PAGE_SIZE = 20;
const SKELETON_ROWS = 6;

function avatarStyle(name: string) {
  const { bg, fg } = avatarColors(name);
  return { backgroundColor: bg, color: fg };
}

const DEACTIVATED_RESET_MESSAGE =
  "Tài khoản này đã bị vô hiệu hoá nên không đặt lại mật khẩu được. Danh sách đã được cập nhật.";
const NO_PASSWORD_RESET_MESSAGE =
  "Tài khoản này chỉ đăng nhập bằng Google, không có mật khẩu để đặt lại. Danh sách đã được cập nhật.";

/**
 * Why UC-07 is unavailable for an account, or null when it is not. Only accounts that sign in with a
 * password can reset one (BA, SCRUM-13) — the backend answers MSG14 for the others as well.
 */
function resetUnavailableReason(account: UserAccount) {
  // No password first: reactivating would not help such an account, so "reactivate first" would mislead.
  if (!account.hasPassword)
    return "Tài khoản chỉ đăng nhập bằng Google — không có mật khẩu để đặt lại";
  if (!account.isActive)
    return "Tài khoản đã bị vô hiệu hoá — kích hoạt lại trước khi đặt lại mật khẩu";
  return null;
}

/**
 * User management (SCRUM-13): list (UC-05), create (UC-06a), edit (UC-06b), deactivate and
 * reactivate (UC-06c), and emailing a password reset link (UC-07). Reaching this screen needs
 * `AdminUsers`; the backend checks it again on every request, so a 403 halfway through is handled
 * as lost access rather than as an error.
 */
export function UsersPage() {
  const { user: me, reloadSession, can } = useAuth();
  // ADR-0060: a Secretary trusted with "Thu giọng mẫu hộ" records, hears back and deletes room-mic samples here.
  const [collecting, setCollecting] = useState(false);
  const toast = useToast();
  const handleAccessLost = useAccessLost();

  const [users, setUsers] = useState<UserAccount[] | null>(null);
  const [roles, setRoles] = useState<RoleRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  // The search and filters live in the URL (as on the meeting list), so a reload, Back or a shared
  // link keeps them. A value that matches nothing — a hand-edited or outdated link — means "all".
  const [searchParams, setSearchParams] = useSearchParams();
  const query = searchParams.get("q") ?? "";
  const roleParam = searchParams.get("role");
  const roleFilter: RoleFilter =
    roleParam && roles?.some((role) => role.id === roleParam) ? roleParam : "all";
  const statusParam = searchParams.get("status");
  const statusFilter: StatusFilter =
    statusParam === "active" || statusParam === "inactive" ? statusParam : "all";
  const pageParam = Number(searchParams.get("page"));
  const pageSizeParam = Number(searchParams.get("pageSize"));
  const pageSize = (PAGE_SIZES as readonly number[]).includes(pageSizeParam)
    ? pageSizeParam
    : DEFAULT_PAGE_SIZE;

  // Replace rather than push: typing a search should not leave a Back step per keystroke.
  const setFilter = useCallback(
    (key: FilterKey, value: string) =>
      setSearchParams(
        () => {
          const next = latestSearchParams();
          if (value && value !== "all") next.set(key, value);
          else next.delete(key);
          // A new criterion starts again from the first page; only paging itself keeps the page.
          if (key !== "page") next.delete("page");
          if (key === "pageSize" && value === String(DEFAULT_PAGE_SIZE)) next.delete("pageSize");
          return next;
        },
        { replace: true },
      ),
    [setSearchParams],
  );

  // The list filters on what is typed straight away; the URL catches up after a pause.
  const [search, setSearch] = useSearchBox(query, (q) => setFilter("q", q.trim()));

  const [formTarget, setFormTarget] = useState<UserFormTarget | null>(null);
  const [statusTarget, setStatusTarget] = useState<UserAccount | null>(null);
  const [resetTarget, setResetTarget] = useState<UserAccount | null>(null);

  const load = useCallback(async () => {
    try {
      const [accounts, availableRoles] = await Promise.all([listUsers(), listRoles()]);
      setUsers(accounts);
      setRoles(availableRoles);
      setLoadError(null);
    } catch (err) {
      if (!handleAccessLost(err)) setLoadError(errorMessage(err));
    }
  }, [handleAccessLost]);

  useEffect(() => {
    // Let load own the request and defer its state updates past the effect's synchronous phase.
    void Promise.resolve().then(load);
  }, [load]);
  // Another administrator's change shows on coming back to the tab; never under an open dialog.
  useRefetchOnFocus(() => void load(), { enabled: !formTarget && !statusTarget && !resetTarget });

  async function openUserForm(target: UserFormTarget) {
    try {
      setRoles(await listRoles());
      setFormTarget(target);
    } catch (error) {
      if (!handleAccessLost(error)) toast.show(errorMessage(error), "error");
    }
  }

  function retry() {
    setLoadError(null);
    setUsers(null);
    void load();
  }

  const patch = useCallback(
    (account: UserAccount) =>
      setUsers((all) => all?.map((u) => (u.id === account.id ? account : u)) ?? all),
    [],
  );

  const visible = useMemo(() => {
    const needle = normalizeSearch(search.trim());
    // Filter only — the backend already sorted with PostgreSQL's Vietnamese collation, which a
    // JavaScript sort would undo ("Đ" would land after "Z").
    return (users ?? []).filter(
      (u) =>
        (roleFilter === "all" || u.roleId === roleFilter) &&
        (statusFilter === "all" || u.isActive === (statusFilter === "active")) &&
        (!needle ||
          normalizeSearch(u.fullName).includes(needle) ||
          normalizeSearch(u.email).includes(needle)),
    );
  }, [users, search, roleFilter, statusFilter]);

  // Every account comes in one response (about a hundred), so the pages are cut here; a page past the end — a stale
  // link, or a search that left fewer rows — shows the last one.
  const totalPages = Math.max(1, Math.ceil(visible.length / pageSize));
  const page = Number.isInteger(pageParam) && pageParam >= 1 ? Math.min(pageParam, totalPages) : 1;
  const pageRows = visible.slice((page - 1) * pageSize, page * pageSize);
  const filtered = Boolean(search.trim()) || roleFilter !== "all" || statusFilter !== "all";

  function clearFilters() {
    setSearch("");
    setSearchParams(
      () => {
        const next = latestSearchParams();
        for (const key of ["q", "role", "status", "page"]) next.delete(key);
        return next;
      },
      { replace: true },
    );
  }

  const handleGone = useCallback(() => {
    setFormTarget(null);
    toast.show("Tài khoản này không còn tồn tại. Danh sách đã được tải lại.", "error");
    void load();
  }, [toast, load]);

  function handleSaved(response: UserMutationResponse) {
    const created = formTarget?.mode === "create";
    setFormTarget(null);
    toast.show(response.message);

    if (created) {
      // Reload rather than insert: only the database knows where the new name sorts.
      void load();
    } else {
      patch(response.user);
      // Editing yourself: re-read name, email and capabilities from /api/auth/me so the shell and
      // the sidebar match what the backend now enforces. A new role keeps the session and the
      // reload picks up its capabilities; a new email has revoked the session on the backend, and
      // the reload notices and ends it here too, rather than minutes later.
      if (response.user.id === me?.id) void reloadSession();
    }
  }

  /**
   * UC-06c. MSG12 has three causes and one code; the backend leaves telling them apart to us.
   * Targeting yourself cannot happen — the button is disabled — so re-read the account: if it is
   * already in the requested state the list was stale, otherwise the change would have left no
   * active administrator.
   */
  async function changeStatus(target: UserAccount) {
    const activate = !target.isActive;
    try {
      const response = await changeUserStatus(target.id, activate);
      patch(response.user);
      toast.show(response.message);
    } catch (err) {
      if (handleAccessLost(err)) return;
      if (isApiError(err, 404)) {
        void load();
        throw new Error("Tài khoản này không còn tồn tại. Danh sách đã được tải lại.");
      }
      if (!isApiError(err, 409, "MSG12")) throw err;

      let fresh: UserAccount;
      try {
        fresh = await getUser(target.id);
      } catch (lookupErr) {
        if (handleAccessLost(lookupErr)) return;
        // Cannot tell the causes apart without the account: say what the backend said.
        throw err;
      }
      patch(fresh);
      if (fresh.isActive === activate) {
        throw new Error(
          activate
            ? "Tài khoản này đã được kích hoạt từ trước. Danh sách đã được cập nhật."
            : "Tài khoản này đã bị vô hiệu hoá từ trước. Danh sách đã được cập nhật.",
        );
      }
      throw new Error(
        activate
          ? errorMessage(err)
          : "Không thể vô hiệu hoá: sau thao tác này sẽ không còn tài khoản quản trị nào đang hoạt động.",
      );
    }
  }

  /**
   * UC-07. Errors stay in the dialog, so the administrator can retry MSG15 from where they are.
   * MSG14 means the list was stale — the account was deactivated, or has no password, by now — so
   * re-read it; the dialog's blocker then keeps the confirm button disabled.
   */
  async function sendPasswordReset(target: UserAccount) {
    try {
      const response = await requestPasswordReset(target.id);
      patch(response.user);
      toast.show(response.message);
    } catch (err) {
      if (handleAccessLost(err)) return;
      if (isApiError(err, 404)) {
        void load();
        throw new Error("Tài khoản này không còn tồn tại. Danh sách đã được tải lại.");
      }
      if (!isApiError(err, 409, "MSG14")) throw err;

      let fresh: UserAccount;
      try {
        fresh = await getUser(target.id);
      } catch (lookupErr) {
        if (handleAccessLost(lookupErr)) return;
        throw err;
      }
      patch(fresh);
      if (!fresh.hasPassword) throw new Error(NO_PASSWORD_RESET_MESSAGE);
      if (!fresh.isActive) throw new Error(DEACTIVATED_RESET_MESSAGE);
      throw err;
    }
  }

  // The row as the list now has it: a reload or a MSG14 re-read may have changed it under the dialog.
  const resetRow = resetTarget && users?.find((u) => u.id === resetTarget.id);

  const activeCount = users?.filter((u) => u.isActive).length ?? 0;
  const roleFilterOptions: { value: RoleFilter; label: string }[] = [
    { value: "all", label: "Tất cả vai trò" },
    ...(roles ?? []).map(({ id, name }) => ({ value: id, label: name })),
  ];

  return (
    <>
      <PageHeader
        title="Người dùng"
        description={users ? `${users.length} tài khoản, ${activeCount} đang hoạt động` : undefined}
        action={
          <>
            {can("VoiceCollector") && (
              <button
                className="btn btn-outline btn-lg"
                type="button"
                onClick={() => setCollecting(true)}
              >
                <Icon name="mic" size={18} />
                Quản lý giọng nói thu hộ
              </button>
            )}
            <button
              className="btn btn-primary btn-lg btn-cta"
              type="button"
              onClick={() => void openUserForm({ mode: "create" })}
              disabled={roles === null}
            >
              <Icon name="userPlus" size={18} />
              Tạo tài khoản
            </button>
          </>
        }
      />

      {collecting && (
        <Dialog title="Quản lý giọng nói thu hộ" size="lg" onClose={() => setCollecting(false)}>
          <CollectForMeeting />
        </Dialog>
      )}

      <div className="list-panel list-page list-fill">
        <div className="toolbar">
          <label className="search search-flex">
            <Icon name="search" size={16} />
            <input
              type="search"
              placeholder="Tìm theo tên hoặc email…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Tìm theo họ tên hoặc email"
            />
          </label>
          <Select
            value={roleFilter}
            options={roleFilterOptions}
            onChange={(value) => setFilter("role", value)}
            label="Lọc theo vai trò"
            steady
          />
          <Select
            value={statusFilter}
            options={STATUS_FILTER_OPTIONS}
            onChange={(value) => setFilter("status", value)}
            label="Lọc theo trạng thái"
            steady
          />
        </div>

        <div className="table-card table-fill">
          <table className="data-table users-table">
            <thead>
              <tr>
                {/* Fixed widths: the columns stay put whichever rows a filter leaves (frontend-ui-design, steady layout). */}
                <th>Họ tên</th>
                <th className="users-col-role">Vai trò</th>
                <th className="users-col-login">Đăng nhập</th>
                <th className="users-col-status">Trạng thái</th>
                <th className="users-col-date">Ngày tạo</th>
                <th className="col-action users-col-action">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {loadError ? (
                <tr>
                  <td colSpan={COLUMN_COUNT} className="cell-empty">
                    <p className="cell-error">{loadError}</p>
                    <button type="button" className="btn btn-outline btn-sm" onClick={retry}>
                      <Icon name="refresh" size={16} />
                      Thử lại
                    </button>
                  </td>
                </tr>
              ) : users === null ? (
                Array.from({ length: SKELETON_ROWS }, (_, i) => (
                  <tr key={i} className="row-skeleton" aria-hidden="true">
                    {Array.from({ length: COLUMN_COUNT }, (_, j) => (
                      <td key={j}>
                        <span className="skeleton" />
                      </td>
                    ))}
                  </tr>
                ))
              ) : visible.length === 0 ? (
                <tr>
                  <td colSpan={COLUMN_COUNT} className="cell-empty">
                    {users.length === 0 || !filtered ? (
                      "Chưa có tài khoản nào."
                    ) : (
                      <>
                        <p className="empty-title">Không có tài khoản nào khớp bộ lọc.</p>
                        <button
                          type="button"
                          className="btn btn-outline btn-sm"
                          onClick={clearFilters}
                        >
                          <Icon name="x" size={16} />
                          Xoá bộ lọc
                        </button>
                      </>
                    )}
                  </td>
                </tr>
              ) : (
                pageRows.map((u) => {
                  const isSelf = u.id === me?.id;
                  const resetUnavailable = resetUnavailableReason(u);
                  return (
                    <tr key={u.id} className={u.isActive ? undefined : "row-inactive"}>
                      <td className="cell-title">
                        <span className="person">
                          <span
                            className="avatar"
                            style={avatarStyle(u.fullName)}
                            aria-hidden="true"
                          >
                            {initials(u.fullName)}
                          </span>
                          <span className="users-person">
                            {/* One line each: a long name or email ends in "…" and reads whole in its tooltip. A phone
                                has no tooltip, so there both wrap — the email before an "@" or a ".", not mid-word. */}
                            <span className="person-name">
                              <span className="users-clip" onPointerOver={tipIfClipped}>
                                {u.fullName}
                              </span>
                              {isSelf && <Badge tone="blue">Bạn</Badge>}
                            </span>
                            <span className="cell-sub users-clip" onPointerOver={tipIfClipped}>
                              {u.email.split(/(?=[@.])/).map((part, i) => (
                                <Fragment key={i}>
                                  {i > 0 && <wbr />}
                                  {part}
                                </Fragment>
                              ))}
                            </span>
                            {/* On a phone the role and status columns go: their facts move under the email. */}
                            <span className="users-inline">
                              <Badge tone="slate">{u.roleName}</Badge>
                              {!u.isActive && <Badge tone="amber">Đã vô hiệu hoá</Badge>}
                            </span>
                          </span>
                        </span>
                      </td>
                      <td className="users-col users-role">
                        <Badge tone="slate">{u.roleName}</Badge>
                      </td>
                      <td className="cell-muted users-col users-login">
                        {[u.hasPassword && "Mật khẩu", u.hasGoogleLink && "Google"]
                          .filter(Boolean)
                          .join(", ")}
                      </td>
                      <td className="users-col">
                        {/* A chip, as on every other list; the exception carries the warm colour. */}
                        {u.isActive ? (
                          <Badge tone="emerald">Hoạt động</Badge>
                        ) : (
                          <Badge tone="amber">Đã vô hiệu hoá</Badge>
                        )}
                      </td>
                      <td className="cell-muted users-col users-date">{formatDate(u.createdAt)}</td>
                      <td className="col-action">
                        {/* On a phone each action says what it does: a touch screen never shows the tooltips. */}
                        <span className="row-actions row-actions-labelled">
                          <button
                            type="button"
                            className="icon-button icon-button-sm"
                            onClick={() => void openUserForm({ mode: "edit", id: u.id })}
                            data-tooltip="Sửa hồ sơ"
                            aria-label={`Sửa tài khoản ${u.fullName}`}
                          >
                            <Icon name="pencil" size={16} />
                            <span className="row-action-label">Sửa</span>
                          </button>
                          <button
                            type="button"
                            className="icon-button icon-button-sm"
                            onClick={() => setStatusTarget(u)}
                            disabled={isSelf}
                            data-tooltip={
                              isSelf
                                ? "Không thể tự đổi trạng thái tài khoản của mình"
                                : u.isActive
                                  ? "Vô hiệu hoá"
                                  : "Kích hoạt lại"
                            }
                            aria-label={`${u.isActive ? "Vô hiệu hoá" : "Kích hoạt lại"} tài khoản ${u.fullName}`}
                          >
                            <Icon name={u.isActive ? "lock" : "unlock"} size={16} />
                            <span className="row-action-label">
                              {u.isActive ? "Vô hiệu hoá" : "Kích hoạt lại"}
                            </span>
                          </button>
                          <button
                            type="button"
                            className="icon-button icon-button-sm"
                            onClick={() => setResetTarget(u)}
                            disabled={resetUnavailable !== null}
                            data-tooltip={resetUnavailable ?? "Gửi liên kết đặt lại mật khẩu"}
                            aria-label={`Gửi liên kết đặt lại mật khẩu cho ${u.fullName}`}
                          >
                            <Icon name="key" size={16} />
                            <span className="row-action-label">Đặt lại mật khẩu</span>
                          </button>
                          {/* SCRUM-113: an active person's library stays theirs; a deactivated one's is read here. */}
                          {u.isActive ? (
                            <button
                              type="button"
                              className="icon-button icon-button-sm"
                              disabled
                              data-tooltip="Chỉ xem được kho của tài khoản đã vô hiệu hoá"
                              aria-label={`Xem kho tài liệu của ${u.fullName}`}
                            >
                              <Icon name="folder" size={16} />
                            </button>
                          ) : (
                            <Link
                              to={routes.userLibrary(u.id)}
                              className="icon-button icon-button-sm"
                              data-tooltip="Xem kho tài liệu"
                              aria-label={`Xem kho tài liệu của ${u.fullName}`}
                            >
                              <Icon name="folder" size={16} />
                              <span className="row-action-label">Xem kho</span>
                            </Link>
                          )}
                        </span>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        {users && visible.length > 0 && !loadError && (
          <Pagination
            page={page}
            pageSize={pageSize}
            totalCount={visible.length}
            totalPages={totalPages}
            pageSizes={PAGE_SIZES}
            itemLabel="tài khoản"
            onPageChange={(next) => setFilter("page", next > 1 ? String(next) : "")}
            onPageSizeChange={(size) => setFilter("pageSize", String(size))}
          />
        )}
      </div>

      {formTarget && (
        <UserFormDialog
          target={formTarget}
          roles={roles ?? []}
          currentUserId={me?.id}
          onSaved={handleSaved}
          onGone={handleGone}
          onAccessLost={handleAccessLost}
          onClose={() => setFormTarget(null)}
        />
      )}

      {statusTarget &&
        (statusTarget.isActive ? (
          <ConfirmDialog
            title="Vô hiệu hoá tài khoản"
            tone="danger"
            confirmLabel="Vô hiệu hoá"
            pendingLabel="Đang vô hiệu hoá…"
            onConfirm={() => changeStatus(statusTarget)}
            onClose={() => setStatusTarget(null)}
          >
            Vô hiệu hoá tài khoản của <b>{statusTarget.fullName}</b>? Người này bị đăng xuất khỏi
            mọi thiết bị và không đăng nhập được cho tới khi được kích hoạt lại. Lịch sử cuộc họp
            của họ vẫn được giữ nguyên.
          </ConfirmDialog>
        ) : (
          <ConfirmDialog
            title="Kích hoạt lại tài khoản"
            confirmLabel="Kích hoạt lại"
            pendingLabel="Đang kích hoạt…"
            onConfirm={() => changeStatus(statusTarget)}
            onClose={() => setStatusTarget(null)}
          >
            Kích hoạt lại tài khoản của <b>{statusTarget.fullName}</b>? Người này sẽ đăng nhập lại
            được với vai trò hiện có.
          </ConfirmDialog>
        ))}

      {resetTarget && (
        <ConfirmDialog
          title="Đặt lại mật khẩu"
          confirmLabel="Gửi liên kết"
          pendingLabel="Đang gửi email…"
          blocker={
            resetRow === undefined
              ? "Tài khoản này không còn tồn tại."
              : resetRow?.hasPassword === false
                ? NO_PASSWORD_RESET_MESSAGE
                : resetRow?.isActive === false
                  ? DEACTIVATED_RESET_MESSAGE
                  : undefined
          }
          onConfirm={() => sendPasswordReset(resetTarget)}
          onClose={() => setResetTarget(null)}
        >
          Gửi liên kết đặt lại mật khẩu tới <b className="wrap-anywhere">{resetTarget.email}</b>?
          Liên kết dùng được một lần trong 60 phút; liên kết gửi trước đó (nếu có) sẽ hết hiệu lực.
          Mật khẩu hiện tại vẫn dùng được cho tới khi {resetTarget.fullName} đặt mật khẩu mới.
          {resetTarget.id === me?.id &&
            " Đây là tài khoản của bạn: khi đặt mật khẩu mới qua liên kết, mọi phiên đăng nhập của bạn sẽ kết thúc."}
        </ConfirmDialog>
      )}
    </>
  );
}
