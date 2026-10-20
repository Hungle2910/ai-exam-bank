'use client';

import React, { useState, useCallback, useEffect } from 'react';
import { Plus, Search, Users, Shield, RefreshCw, MoreHorizontal, UserCheck, UserX } from 'lucide-react';
import { useAuth, AppRole } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
import clsx from 'clsx';

/* ============================================================
   TYPES
   ============================================================ */

interface School { id: string; name: string; }
interface RoleInfo { role: AppRole; departmentId: string | null; }
interface AppUser {
  id: string;
  userName: string;
  schoolId: string;
  roles: RoleInfo[];
}

/* ============================================================
   ROLE CONFIG
   ============================================================ */

const ROLE_LABEL: Record<AppRole, string> = {
  MinistryAdmin: 'Quản trị Bộ',
  SchoolAdmin: 'Quản trị Trường',
  DepartmentHead: 'Trưởng Bộ Môn',
  Teacher: 'Giáo viên',
};

const ROLE_BADGE: Record<AppRole, string> = {
  MinistryAdmin: 'badge-purple',
  SchoolAdmin: 'badge-blue',
  DepartmentHead: 'badge-teal',
  Teacher: 'badge-green',
};

const ASSIGNABLE_ROLES: AppRole[] = ['SchoolAdmin', 'DepartmentHead', 'Teacher'];

/* ============================================================
   USERS PAGE
   ============================================================ */

export default function UsersPage() {
  const { csrfToken, primaryRole } = useAuth();
  const { success, error: showError } = useToast();

  const [schools, setSchools] = useState<School[]>([]);
  const [selectedSchool, setSelectedSchool] = useState<string>('');
  const [users, setUsers] = useState<AppUser[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState('');
  const [showCreateModal, setShowCreateModal] = useState(false);

  // Load schools
  useEffect(() => {
    const load = async () => {
      try {
        const res = await fetch('/api/v1/admin/schools', { credentials: 'include' });
        if (res.ok) {
          const data: School[] = await res.json();
          setSchools(data);
          if (data.length > 0) setSelectedSchool(data[0].id);
        }
      } catch { /* ignore */ }
    };
    load();
  }, []);

  // Load users when school changes
  const fetchUsers = useCallback(async (schoolId: string) => {
    if (!schoolId) return;
    setLoading(true);
    try {
      const res = await fetch(`/api/v1/admin/schools/${schoolId}/users`, { credentials: 'include' });
      if (res.ok) {
        setUsers(await res.json());
      } else {
        showError('Không thể tải danh sách người dùng');
      }
    } catch {
      showError('Lỗi kết nối');
    } finally {
      setLoading(false);
    }
  }, [showError]);

  useEffect(() => {
    if (selectedSchool) fetchUsers(selectedSchool);
  }, [selectedSchool, fetchUsers]);

  const filtered = users.filter(u =>
    u.userName.toLowerCase().includes(search.toLowerCase())
  );

  const currentSchoolName = schools.find(s => s.id === selectedSchool)?.name ?? '';

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Người dùng</h1>
        <p className="page-subtitle">Quản lý tài khoản và phân quyền trong trường</p>
        <div className="page-actions">
          <select
            className="form-select"
            style={{ width: 260 }}
            value={selectedSchool}
            onChange={e => setSelectedSchool(e.target.value)}
            id="school-selector"
            aria-label="Select school"
          >
            {schools.map(s => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </select>
          <button className="btn btn-secondary" onClick={() => fetchUsers(selectedSchool)} id="btn-refresh-users">
            <RefreshCw size={14} />
          </button>
          <button className="btn btn-primary" onClick={() => setShowCreateModal(true)} id="btn-create-user">
            <Plus size={14} />
            Thêm người dùng
          </button>
        </div>
      </div>

      {/* Table */}
      <div className="table-container">
        <div className="table-toolbar">
          <div className="search-bar" style={{ width: 280 }}>
            <Search size={14} className="search-bar-icon" />
            <input
              type="text"
              placeholder="Tìm người dùng..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              id="user-search"
            />
          </div>
          <span style={{ marginLeft: 'auto', fontSize: 12.5, color: 'var(--gray-400)' }}>
            {filtered.length} người dùng
          </span>
        </div>

        {loading ? (
          <SkeletonTable rows={6} />
        ) : filtered.length === 0 ? (
          <div className="table-empty">
            <div className="table-empty-icon"><Users /></div>
            <div className="table-empty-text">
              {search ? 'Không tìm thấy người dùng' : 'Chưa có người dùng nào'}
            </div>
            <div className="table-empty-sub">
              {search ? 'Thử tìm kiếm khác' : `Thêm người dùng đầu tiên cho ${currentSchoolName}`}
            </div>
          </div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Người dùng</th>
                <th>Vai trò</th>
                <th>Trạng thái</th>
                <th style={{ width: 80 }}></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(u => (
                <tr key={u.id}>
                  <td>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                      <UserAvatar name={u.userName} />
                      <div>
                        <div style={{ fontWeight: 600, color: 'var(--gray-900)' }}>{u.userName}</div>
                        <div style={{ fontSize: 11.5, color: 'var(--gray-400)' }}>
                          {u.id.slice(0, 12)}…
                        </div>
                      </div>
                    </div>
                  </td>
                  <td>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                      {u.roles.length > 0 ? u.roles.map((r, i) => (
                        <span key={i} className={clsx('badge', ROLE_BADGE[r.role])}>
                          <span className="badge-dot" />
                          {ROLE_LABEL[r.role]}
                        </span>
                      )) : (
                        <span className="badge badge-gray">Chưa phân quyền</span>
                      )}
                    </div>
                  </td>
                  <td>
                    <span className="badge badge-green">
                      <span className="badge-dot" />
                      Hoạt động
                    </span>
                  </td>
                  <td>
                    <UserRowMenu userId={u.id} schoolId={selectedSchool} csrfToken={csrfToken} onRefresh={() => fetchUsers(selectedSchool)} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {showCreateModal && (
        <CreateUserModal
          schools={schools}
          defaultSchoolId={selectedSchool}
          csrfToken={csrfToken}
          onClose={() => setShowCreateModal(false)}
          onCreated={() => {
            setShowCreateModal(false);
            fetchUsers(selectedSchool);
            success('Đã tạo người dùng thành công');
          }}
        />
      )}
    </div>
  );
}

/* ============================================================
   AVATAR
   ============================================================ */

function UserAvatar({ name }: { name: string }) {
  const initials = name.slice(0, 2).toUpperCase();
  const hue = name.charCodeAt(0) * 13 % 360;
  return (
    <div style={{
      width: 32, height: 32, borderRadius: '50%',
      background: `hsl(${hue}, 65%, 55%)`,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontSize: 12, fontWeight: 700, color: 'white', flexShrink: 0,
    }}>
      {initials}
    </div>
  );
}

/* ============================================================
   USER ROW MENU
   ============================================================ */

function UserRowMenu({ userId, schoolId, csrfToken, onRefresh }: {
  userId: string; schoolId: string; csrfToken: string | null; onRefresh: () => void;
}) {
  const [open, setOpen] = useState(false);
  return (
    <div className="dropdown-trigger">
      <button className="topbar-icon-btn" onClick={() => setOpen(v => !v)} id={`btn-user-menu-${userId}`} aria-label="User options">
        <MoreHorizontal size={15} />
      </button>
      {open && (
        <>
          <div style={{ position: 'fixed', inset: 0, zIndex: 70 }} onClick={() => setOpen(false)} />
          <div className="dropdown-menu">
            <div className="dropdown-item"><UserCheck size={14} />Thêm quyền</div>
            <div className="dropdown-item"><Shield size={14} />Xem audit</div>
            <div className="dropdown-divider" />
            <div className="dropdown-item danger"><UserX size={14} />Vô hiệu hóa</div>
          </div>
        </>
      )}
    </div>
  );
}

/* ============================================================
   CREATE USER MODAL
   ============================================================ */

interface CreateUserModalProps {
  schools: School[];
  defaultSchoolId: string;
  csrfToken: string | null;
  onClose: () => void;
  onCreated: () => void;
}

function CreateUserModal({ schools, defaultSchoolId, csrfToken, onClose, onCreated }: CreateUserModalProps) {
  const [form, setForm] = useState({
    userName: '',
    password: '',
    confirmPassword: '',
    role: 'Teacher' as AppRole,
    schoolId: defaultSchoolId,
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(false);

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.userName.trim() || form.userName.length < 3) e.userName = 'Tên đăng nhập tối thiểu 3 ký tự';
    if (form.userName.length > 256) e.userName = 'Tối đa 256 ký tự';
    if (!form.password || form.password.length < 8) e.password = 'Mật khẩu tối thiểu 8 ký tự';
    if (form.password !== form.confirmPassword) e.confirmPassword = 'Mật khẩu không khớp';
    if (!form.schoolId) e.schoolId = 'Vui lòng chọn trường';
    return e;
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    const errs = validate();
    if (Object.keys(errs).length > 0) { setErrors(errs); return; }
    if (!csrfToken) return;

    setLoading(true);
    try {
      const res = await fetch(`/api/v1/admin/schools/${form.schoolId}/users`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken },
        body: JSON.stringify({
          userName: form.userName.trim(),
          password: form.password,
          role: form.role,
        }),
      });

      if (res.status === 201) {
        onCreated();
      } else if (res.status === 409) {
        setErrors({ userName: 'Tên đăng nhập này đã tồn tại' });
      } else if (res.status === 400) {
        const data = await res.json().catch(() => null);
        setErrors({ general: 'Dữ liệu không hợp lệ. Vui lòng kiểm tra lại' });
      } else {
        setErrors({ general: 'Đã có lỗi xảy ra. Vui lòng thử lại' });
      }
    } catch {
      setErrors({ general: 'Lỗi kết nối máy chủ' });
    } finally {
      setLoading(false);
    }
  };

  const setField = (key: string, val: string) => {
    setForm(prev => ({ ...prev, [key]: val }));
    setErrors(prev => { const e = { ...prev }; delete e[key]; delete e.general; return e; });
  };

  return (
    <div className="modal-overlay" onClick={e => e.target === e.currentTarget && onClose()}>
      <div className="modal modal-lg">
        <div className="modal-header">
          <div>
            <div className="modal-title">Thêm người dùng mới</div>
            <div className="modal-subtitle">Tạo tài khoản và gán vai trò ngay lập tức</div>
          </div>
          <button className="modal-close" onClick={onClose} id="btn-close-create-user">✕</button>
        </div>
        <form onSubmit={submit}>
          <div className="modal-body" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0 16px' }}>
            {errors.general && (
              <div style={{ gridColumn: '1/-1', background: 'var(--danger-50)', border: '1px solid var(--danger-200)', borderRadius: 8, padding: '10px 14px', fontSize: 13, color: 'var(--danger-700)', marginBottom: 8 }}>
                ⚠ {errors.general}
              </div>
            )}

            {/* Username */}
            <div className="form-group" style={{ gridColumn: '1/-1' }}>
              <label className="form-label" htmlFor="new-username">Tên đăng nhập <span className="required">*</span></label>
              <input id="new-username" className={clsx('form-input', errors.userName && 'error')} placeholder="VD: gv.nguyen.van.a" value={form.userName} onChange={e => setField('userName', e.target.value)} autoFocus />
              {errors.userName && <div className="form-error">⚠ {errors.userName}</div>}
            </div>

            {/* Password */}
            <div className="form-group">
              <label className="form-label" htmlFor="new-password">Mật khẩu <span className="required">*</span></label>
              <input id="new-password" type="password" className={clsx('form-input', errors.password && 'error')} placeholder="Tối thiểu 8 ký tự" value={form.password} onChange={e => setField('password', e.target.value)} />
              {errors.password && <div className="form-error">⚠ {errors.password}</div>}
            </div>

            {/* Confirm */}
            <div className="form-group">
              <label className="form-label" htmlFor="confirm-password">Xác nhận mật khẩu <span className="required">*</span></label>
              <input id="confirm-password" type="password" className={clsx('form-input', errors.confirmPassword && 'error')} placeholder="Nhập lại mật khẩu" value={form.confirmPassword} onChange={e => setField('confirmPassword', e.target.value)} />
              {errors.confirmPassword && <div className="form-error">⚠ {errors.confirmPassword}</div>}
            </div>

            {/* School */}
            <div className="form-group">
              <label className="form-label" htmlFor="user-school">Trường <span className="required">*</span></label>
              <select id="user-school" className={clsx('form-select', errors.schoolId && 'error')} value={form.schoolId} onChange={e => setField('schoolId', e.target.value)}>
                {schools.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
              {errors.schoolId && <div className="form-error">⚠ {errors.schoolId}</div>}
            </div>

            {/* Role */}
            <div className="form-group">
              <label className="form-label" htmlFor="user-role">Vai trò <span className="required">*</span></label>
              <select id="user-role" className="form-select" value={form.role} onChange={e => setField('role', e.target.value)}>
                {ASSIGNABLE_ROLES.map(r => (
                  <option key={r} value={r}>{ROLE_LABEL[r]}</option>
                ))}
              </select>
            </div>
          </div>
          <div className="modal-footer">
            <button type="button" className="btn btn-secondary" onClick={onClose} id="btn-cancel-user">Hủy</button>
            <button type="submit" className={clsx('btn btn-primary', loading && 'btn-loading')} disabled={loading} id="btn-submit-user">
              {loading ? 'Đang tạo…' : 'Tạo người dùng'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ============================================================
   SKELETON TABLE
   ============================================================ */

function SkeletonTable({ rows }: { rows: number }) {
  return (
    <table>
      <thead>
        <tr>
          {['Người dùng', 'Vai trò', 'Trạng thái', ''].map((h, i) => (
            <th key={i}><div className="skeleton skeleton-text" style={{ width: 80 }} /></th>
          ))}
        </tr>
      </thead>
      <tbody>
        {Array.from({ length: rows }).map((_, r) => (
          <tr key={r}>
            <td>
              <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
                <div className="skeleton" style={{ width: 32, height: 32, borderRadius: '50%' }} />
                <div>
                  <div className="skeleton skeleton-text" style={{ width: 140 }} />
                  <div className="skeleton skeleton-text sm" style={{ width: 90, marginTop: 4 }} />
                </div>
              </div>
            </td>
            <td><div className="skeleton skeleton-text" style={{ width: 90 }} /></td>
            <td><div className="skeleton skeleton-text" style={{ width: 70 }} /></td>
            <td></td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
