'use client';

import React, { useState, useCallback, useEffect, useRef } from 'react';
import {
  Shield, Plus, Search, RefreshCw, Trash2
} from 'lucide-react';
import { useAuth, AppRole } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
import clsx from 'clsx';

/* ============================================================
   TYPES
   ============================================================ */

interface School { id: string; name: string; }
interface Department { id: string; schoolId: string; name: string; }

interface RoleAssignment {
  id: string;
  userId: string;
  role: AppRole;
  schoolId: string | null;
  departmentId: string | null;
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

const ROLE_DESC: Record<AppRole, string> = {
  MinistryAdmin: 'Toàn bộ hệ thống, không giới hạn trường',
  SchoolAdmin: 'Quản lý một trường học cụ thể',
  DepartmentHead: 'Quản lý bộ môn, duyệt câu hỏi',
  Teacher: 'Soạn và gửi câu hỏi trong phạm vi được giao',
};

/* ============================================================
   ROLES PAGE
   ============================================================ */

export default function RolesPage() {
  const { csrfToken, primaryRole, apiFetch } = useAuth();
  const { success, error: showError } = useToast();

  const [schools, setSchools] = useState<School[]>([]);
  const [selectedSchool, setSelectedSchool] = useState('');
  const [assignments, setAssignments] = useState<RoleAssignment[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState('');
  const [showGrantModal, setShowGrantModal] = useState(false);
  const assignmentRequest = useRef(0);

  const fetchAssignments = useCallback(async (schoolId: string) => {
    if (!schoolId) return;
    const request = ++assignmentRequest.current;
    setLoading(true);
    try {
      const res = await apiFetch(`/api/v1/admin/schools/${schoolId}/role-assignments`);
      if (res.ok) {
        const items = await res.json();
        if (request === assignmentRequest.current) setAssignments(items);
      } else if (request === assignmentRequest.current) showError('Không thể tải phân quyền');
    } catch {
      if (request === assignmentRequest.current) showError('Lỗi kết nối');
    } finally {
      if (request === assignmentRequest.current) setLoading(false);
    }
  }, [apiFetch, showError]);

  useEffect(() => {
    const load = async () => {
      try {
        const res = await apiFetch('/api/v1/admin/schools');
        if (res.ok) {
          const data: School[] = await res.json();
          setSchools(data);
          if (data.length > 0) {
            setSelectedSchool(data[0].id);
            await fetchAssignments(data[0].id);
          }
        }
      } catch { /* ignore */ }
    };
    void load();
  }, [apiFetch, fetchAssignments]);

  const revokeRole = useCallback(async (assignmentId: string) => {
    if (!csrfToken) return;
    try {
      const res = await apiFetch(`/api/v1/admin/role-assignments/${assignmentId}`, {
        method: 'DELETE',
        credentials: 'include',
        headers: { 'X-CSRF-TOKEN': csrfToken },
      });
      if (res.status === 204) {
        setAssignments(prev => prev.filter(a => a.id !== assignmentId));
        success('Đã thu hồi quyền thành công');
      } else if (res.status === 403) {
        showError('Bạn không có quyền thu hồi phân quyền này');
      } else {
        showError('Không thể thu hồi quyền. Thử lại sau');
      }
    } catch { showError('Lỗi kết nối'); }
  }, [apiFetch, csrfToken, success, showError]);

  const filtered = assignments.filter(a =>
    a.userId.toLowerCase().includes(search.toLowerCase()) ||
    ROLE_LABEL[a.role].toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Phân quyền</h1>
        <p className="page-subtitle">Xem và quản lý vai trò người dùng trong từng trường</p>
        <div className="page-actions">
          <select
            className="form-select"
            style={{ width: 260 }}
            value={selectedSchool}
            onChange={e => {
              setSelectedSchool(e.target.value);
              setAssignments([]);
              void fetchAssignments(e.target.value);
            }}
            id="roles-school-selector"
          >
            {schools.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
          <button className="btn btn-secondary" onClick={() => fetchAssignments(selectedSchool)} id="btn-refresh-roles">
            <RefreshCw size={14} />
          </button>
          <button className="btn btn-primary" onClick={() => setShowGrantModal(true)} id="btn-grant-role">
            <Plus size={14} />
            Gán quyền
          </button>
        </div>
      </div>

      {/* Role overview cards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 12, marginBottom: 24 }}>
        {(Object.keys(ROLE_LABEL) as AppRole[]).map(role => {
          const count = assignments.filter(a => a.role === role).length;
          return (
            <div key={role} className="card" style={{ padding: '16px 18px' }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
                <span className={clsx('badge', ROLE_BADGE[role])}>
                  <span className="badge-dot" />{ROLE_LABEL[role]}
                </span>
                <span style={{ fontSize: 20, fontWeight: 800, color: 'var(--gray-800)' }}>{count}</span>
              </div>
              <div style={{ fontSize: 11.5, color: 'var(--gray-400)', lineHeight: 1.5 }}>
                {ROLE_DESC[role]}
              </div>
            </div>
          );
        })}
      </div>

      {/* Table */}
      <div className="table-container">
        <div className="table-toolbar">
          <div className="search-bar" style={{ width: 280 }}>
            <Search size={14} className="search-bar-icon" />
            <input
              type="text"
              placeholder="Tìm theo user ID hoặc vai trò..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              id="roles-search"
            />
          </div>
          <span style={{ marginLeft: 'auto', fontSize: 12.5, color: 'var(--gray-400)' }}>
            {filtered.length} phân quyền
          </span>
        </div>

        {loading ? (
          <SkeletonTable rows={5} />
        ) : filtered.length === 0 ? (
          <div className="table-empty">
            <div className="table-empty-icon"><Shield /></div>
            <div className="table-empty-text">Chưa có phân quyền nào</div>
            <div className="table-empty-sub">Bắt đầu bằng cách gán quyền cho người dùng</div>
          </div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>User ID</th>
                <th>Vai trò</th>
                <th>Bộ môn</th>
                <th>Phạm vi</th>
                <th style={{ width: 60 }}></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(a => (
                <tr key={a.id}>
                  <td>
                    <code style={{ fontSize: 11.5, color: 'var(--gray-600)', background: 'var(--gray-100)', padding: '2px 8px', borderRadius: 4 }}>
                      {a.userId.slice(0, 12)}…
                    </code>
                  </td>
                  <td>
                    <span className={clsx('badge', ROLE_BADGE[a.role])}>
                      <span className="badge-dot" />
                      {ROLE_LABEL[a.role]}
                    </span>
                  </td>
                  <td style={{ color: 'var(--gray-500)', fontSize: 12.5 }}>
                    {a.departmentId ? <code style={{ fontSize: 11, background: 'var(--gray-100)', padding: '2px 6px', borderRadius: 4 }}>{a.departmentId.slice(0, 8)}…</code> : '—'}
                  </td>
                  <td>
                    {a.schoolId ? (
                      <span className="badge badge-gray" style={{ fontSize: 11.5 }}>Cấp trường</span>
                    ) : (
                      <span className="badge badge-purple" style={{ fontSize: 11.5 }}>Cấp Bộ</span>
                    )}
                  </td>
                  <td>
                    <RevokeButton assignmentId={a.id} onRevoke={revokeRole} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {showGrantModal && (
        <GrantRoleModal
          schools={schools}
          defaultSchoolId={selectedSchool}
          csrfToken={csrfToken}
          primaryRole={primaryRole}
          onClose={() => setShowGrantModal(false)}
          onGranted={() => {
            setShowGrantModal(false);
            fetchAssignments(selectedSchool);
            success('Đã gán quyền thành công');
          }}
        />
      )}
    </div>
  );
}

/* ============================================================
   REVOKE BUTTON with CONFIRM
   ============================================================ */

function RevokeButton({ assignmentId, onRevoke }: { assignmentId: string; onRevoke: (id: string) => void }) {
  const [confirm, setConfirm] = useState(false);

  if (confirm) {
    return (
      <div style={{ display: 'flex', gap: 4 }}>
        <button
          className="btn btn-danger btn-sm"
          onClick={() => { onRevoke(assignmentId); setConfirm(false); }}
          id={`btn-confirm-revoke-${assignmentId}`}
        >
          Xác nhận
        </button>
        <button className="btn btn-ghost btn-sm" onClick={() => setConfirm(false)}>Hủy</button>
      </div>
    );
  }

  return (
    <button
      className="topbar-icon-btn"
      style={{ color: 'var(--danger-400)' }}
      onClick={() => setConfirm(true)}
      id={`btn-revoke-${assignmentId}`}
      aria-label="Revoke role"
    >
      <Trash2 size={14} />
    </button>
  );
}

/* ============================================================
   GRANT ROLE MODAL
   ============================================================ */

interface GrantRoleModalProps {
  schools: School[];
  defaultSchoolId: string;
  csrfToken: string | null;
  primaryRole: AppRole | null;
  onClose: () => void;
  onGranted: () => void;
}

function GrantRoleModal({ schools, defaultSchoolId, csrfToken, primaryRole, onClose, onGranted }: GrantRoleModalProps) {
  const { apiFetch } = useAuth();
  const assignableRoles: AppRole[] = primaryRole === 'MinistryAdmin' ? ['SchoolAdmin'] : ['DepartmentHead', 'Teacher'];
  const [userId, setUserId] = useState('');
  const [role, setRole] = useState<AppRole>(assignableRoles[0]);
  const [schoolId, setSchoolId] = useState(defaultSchoolId);
  const [departmentId, setDepartmentId] = useState('');
  const [departments, setDepartments] = useState<Department[]>([]);
  const [loadingDepartments, setLoadingDepartments] = useState(assignableRoles[0] !== 'SchoolAdmin');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const { error: showError } = useToast();
  const selectedSchoolId = schools.some(school => school.id === schoolId)
    ? schoolId
    : schools.find(school => school.id === defaultSchoolId)?.id ?? schools[0]?.id ?? '';

  useEffect(() => {
    if (!selectedSchoolId || role === 'SchoolAdmin') return;
    let active = true;
    apiFetch(`/api/v1/admin/schools/${selectedSchoolId}/departments`)
      .then(async response => {
        if (!response.ok) throw new Error('Không thể tải bộ môn');
        return response.json() as Promise<Department[]>;
      })
      .then(items => { if (active) setDepartments(items); })
      .catch(() => { if (active) setError('Không thể tải bộ môn'); })
      .finally(() => { if (active) setLoadingDepartments(false); });
    return () => { active = false; };
  }, [apiFetch, selectedSchoolId, role]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!userId.trim()) { setError('Vui lòng nhập User ID'); return; }
    if (!selectedSchoolId) { setError('Vui lòng chọn trường'); return; }
    if (role !== 'SchoolAdmin' && !departmentId) { setError('Vui lòng chọn bộ môn'); return; }
    if (!csrfToken) return;

    setLoading(true);
    setError('');
    try {
      const res = await apiFetch(`/api/v1/admin/users/${userId.trim()}/role-assignments`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken },
        body: JSON.stringify({ role, schoolId: selectedSchoolId, departmentId: role === 'SchoolAdmin' ? null : departmentId }),
      });

      if (res.status === 201) {
        onGranted();
      } else if (res.status === 409) {
        setError('Người dùng đã có quyền này');
      } else if (res.status === 403) {
        setError('Bạn không có quyền thực hiện thao tác này');
      } else if (res.status === 404) {
        setError('Không tìm thấy người dùng');
      } else {
        setError('Đã có lỗi xảy ra. Vui lòng thử lại');
      }
    } catch { showError('Lỗi kết nối'); }
    finally { setLoading(false); }
  };

  return (
    <div className="modal-overlay" onClick={e => e.target === e.currentTarget && onClose()}>
      <div className="modal">
        <div className="modal-header">
          <div>
            <div className="modal-title">Gán vai trò</div>
            <div className="modal-subtitle">Cấp quyền cho người dùng trong hệ thống</div>
          </div>
          <button className="modal-close" onClick={onClose} id="btn-close-grant-role">✕</button>
        </div>
        <form onSubmit={submit}>
          <div className="modal-body">
            {error && (
              <div style={{ background: 'var(--danger-50)', border: '1px solid var(--danger-200)', borderRadius: 8, padding: '10px 14px', fontSize: 13, color: 'var(--danger-700)', marginBottom: 16 }}>
                ⚠ {error}
              </div>
            )}

            <div className="form-group">
              <label className="form-label" htmlFor="grant-userid">User ID <span className="required">*</span></label>
              <input
                id="grant-userid"
                className="form-input"
                placeholder="GUID của người dùng"
                value={userId}
                onChange={e => { setUserId(e.target.value); setError(''); }}
              />
              <div className="form-hint">Lấy từ trang Người dùng</div>
            </div>

            <div className="form-group">
              <label className="form-label" htmlFor="grant-role">Vai trò <span className="required">*</span></label>
              <select id="grant-role" className="form-select" value={role} onChange={e => {
                setRole(e.target.value as AppRole);
                setDepartmentId('');
                setDepartments([]);
                setLoadingDepartments(e.target.value !== 'SchoolAdmin');
              }}>
                {assignableRoles.map(r => (
                  <option key={r} value={r}>{ROLE_LABEL[r]}</option>
                ))}
              </select>
              <div className="form-hint" style={{ marginTop: 4 }}>
                {ROLE_DESC[role]}
              </div>
            </div>

            <div className="form-group">
              <label className="form-label" htmlFor="grant-school">Trường</label>
              <select id="grant-school" className="form-select" value={selectedSchoolId} onChange={e => {
                setSchoolId(e.target.value);
                setDepartmentId('');
                setDepartments([]);
                setLoadingDepartments(true);
              }}>
                {schools.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            </div>
            {role !== 'SchoolAdmin' && <div className="form-group">
              <label className="form-label" htmlFor="grant-department">Bộ môn <span className="required">*</span></label>
              <select id="grant-department" className="form-select" value={departmentId}
                onChange={e => setDepartmentId(e.target.value)} disabled={loadingDepartments}>
                <option value="">— Chọn bộ môn —</option>
                {departments.map(department => <option key={department.id} value={department.id}>{department.name}</option>)}
              </select>
              {departments.length === 0 && !loadingDepartments && <div className="form-hint">Chưa có bộ môn. Vào Trường học → Bộ môn để thêm.</div>}
            </div>}
          </div>
          <div className="modal-footer">
            <button type="button" className="btn btn-secondary" onClick={onClose} id="btn-cancel-grant">Hủy</button>
            <button type="submit" className={clsx('btn btn-primary', loading && 'btn-loading')} disabled={loading} id="btn-submit-grant">
              {loading ? 'Đang gán…' : 'Gán quyền'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ============================================================
   SKELETON
   ============================================================ */

function SkeletonTable({ rows }: { rows: number }) {
  return (
    <table>
      <thead><tr>{['User ID', 'Vai trò', 'Bộ môn', 'Phạm vi', ''].map((_, i) => (
        <th key={i}><div className="skeleton skeleton-text" style={{ width: 70 }} /></th>
      ))}</tr></thead>
      <tbody>{Array.from({ length: rows }).map((_, r) => (
        <tr key={r}>{[140, 100, 80, 70, 30].map((w, c) => (
          <td key={c}><div className="skeleton skeleton-text" style={{ width: w }} /></td>
        ))}</tr>
      ))}</tbody>
    </table>
  );
}
