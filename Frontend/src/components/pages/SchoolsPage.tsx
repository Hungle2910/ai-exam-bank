'use client';

import React, { useState, useCallback, useEffect } from 'react';
import {
  Plus, Search, MoreHorizontal, Edit2, Trash2, Building2, Users, ChevronRight, RefreshCw
} from 'lucide-react';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
import clsx from 'clsx';

/* ============================================================
   TYPES
   ============================================================ */

interface School {
  id: string;
  name: string;
}

interface Department {
  id: string;
  schoolId: string;
  name: string;
}

/* ============================================================
   SCHOOLS PAGE
   ============================================================ */

export default function SchoolsPage() {
  const { csrfToken } = useAuth();
  const { success, error: showError } = useToast();

  const [schools, setSchools] = useState<School[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');

  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showDepartments, setShowDepartments] = useState<string | null>(null);

  const fetchSchools = useCallback(async () => {
    setLoading(true);
    try {
      const res = await fetch('/api/v1/admin/schools', { credentials: 'include' });
      if (res.ok) {
        const data = await res.json();
        setSchools(data);
      } else {
        showError('Không thể tải danh sách trường');
      }
    } catch {
      showError('Lỗi kết nối máy chủ');
    } finally {
      setLoading(false);
    }
  }, [showError]);

  useEffect(() => { fetchSchools(); }, [fetchSchools]);

  const filtered = schools.filter(s =>
    s.name.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Trường học</h1>
        <p className="page-subtitle">Quản lý danh sách trường và bộ môn trong hệ thống</p>
        <div className="page-actions">
          <button
            className="btn btn-secondary"
            onClick={fetchSchools}
            id="btn-refresh-schools"
          >
            <RefreshCw size={14} />
            Làm mới
          </button>
          <button
            className="btn btn-primary"
            onClick={() => setShowCreateModal(true)}
            id="btn-create-school"
          >
            <Plus size={14} />
            Thêm trường
          </button>
        </div>
      </div>

      <div className="table-container">
        <div className="table-toolbar">
          <div className="search-bar" style={{ width: 280 }}>
            <Search size={14} className="search-bar-icon" />
            <input
              type="text"
              placeholder="Tìm trường theo tên..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              id="school-search"
              aria-label="Search schools"
            />
          </div>
          <span style={{ marginLeft: 'auto', fontSize: 12.5, color: 'var(--gray-400)' }}>
            {filtered.length} trường
          </span>
        </div>

        {loading ? (
          <SkeletonTable rows={5} cols={3} />
        ) : filtered.length === 0 ? (
          <div className="table-empty">
            <div className="table-empty-icon"><Building2 /></div>
            <div className="table-empty-text">
              {search ? 'Không tìm thấy trường nào' : 'Chưa có trường nào'}
            </div>
            <div className="table-empty-sub">
              {search ? 'Thử tìm kiếm khác' : 'Bắt đầu bằng cách thêm trường đầu tiên'}
            </div>
          </div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Tên trường</th>
                <th>ID</th>
                <th style={{ width: 120 }}>Bộ môn</th>
                <th style={{ width: 80 }}></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(school => (
                <React.Fragment key={school.id}>
                  <tr>
                    <td>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                        <div style={{
                          width: 32, height: 32,
                          background: 'var(--brand-50)',
                          borderRadius: 8,
                          display: 'flex', alignItems: 'center', justifyContent: 'center',
                          color: 'var(--brand-600)', flexShrink: 0
                        }}>
                          <Building2 size={15} />
                        </div>
                        <span style={{ fontWeight: 600, color: 'var(--gray-900)' }}>{school.name}</span>
                      </div>
                    </td>
                    <td>
                      <code style={{ fontSize: 11, color: 'var(--gray-400)', background: 'var(--gray-100)', padding: '2px 6px', borderRadius: 4 }}>
                        {school.id.slice(0, 8)}…
                      </code>
                    </td>
                    <td>
                      <button
                        className="btn btn-ghost btn-sm"
                        onClick={() => setShowDepartments(showDepartments === school.id ? null : school.id)}
                        id={`btn-expand-${school.id}`}
                      >
                        <ChevronRight
                          size={14}
                          style={{
                            transform: showDepartments === school.id ? 'rotate(90deg)' : 'none',
                            transition: 'transform 0.15s ease'
                          }}
                        />
                        Bộ môn
                      </button>
                    </td>
                    <td>
                      <SchoolRowMenu
                        schoolId={school.id}
                        csrfToken={csrfToken}
                        onRefresh={fetchSchools}
                      />
                    </td>
                  </tr>
                  {showDepartments === school.id && (
                    <tr>
                      <td colSpan={4} style={{ padding: 0, background: 'var(--gray-25)' }}>
                        <DepartmentSubTable schoolId={school.id} csrfToken={csrfToken} />
                      </td>
                    </tr>
                  )}
                </React.Fragment>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {showCreateModal && (
        <CreateSchoolModal
          csrfToken={csrfToken}
          onClose={() => setShowCreateModal(false)}
          onCreated={() => { setShowCreateModal(false); fetchSchools(); success('Đã tạo trường thành công'); }}
        />
      )}
    </div>
  );
}

/* ============================================================
   SCHOOL ROW MENU
   ============================================================ */

function SchoolRowMenu({ schoolId, csrfToken, onRefresh }: { schoolId: string; csrfToken: string | null; onRefresh: () => void }) {
  const [open, setOpen] = useState(false);
  return (
    <div className="dropdown-trigger">
      <button
        className="topbar-icon-btn"
        onClick={() => setOpen(v => !v)}
        id={`btn-menu-school-${schoolId}`}
        aria-label="More options"
      >
        <MoreHorizontal size={15} />
      </button>
      {open && (
        <>
          <div style={{ position: 'fixed', inset: 0, zIndex: 70 }} onClick={() => setOpen(false)} />
          <div className="dropdown-menu">
            <div className="dropdown-item">
              <Edit2 size={14} />
              Đổi tên
            </div>
            <div className="dropdown-item">
              <Users size={14} />
              Xem người dùng
            </div>
          </div>
        </>
      )}
    </div>
  );
}

/* ============================================================
   DEPARTMENT SUB-TABLE
   ============================================================ */

function DepartmentSubTable({ schoolId, csrfToken }: { schoolId: string; csrfToken: string | null }) {
  const [depts, setDepts] = useState<Department[]>([]);
  const [loading, setLoading] = useState(true);
  const [showAdd, setShowAdd] = useState(false);
  const { success, error: showError } = useToast();

  useEffect(() => {
    const load = async () => {
      try {
        const res = await fetch(`/api/v1/admin/schools/${schoolId}/departments`, { credentials: 'include' });
        if (res.ok) setDepts(await res.json());
      } catch { /* ignore */ }
      finally { setLoading(false); }
    };
    load();
  }, [schoolId]);

  const addDept = async (name: string) => {
    if (!csrfToken) return;
    try {
      const res = await fetch(`/api/v1/admin/schools/${schoolId}/departments`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken },
        body: JSON.stringify({ name }),
      });
      if (res.status === 201) {
        const data = await res.json();
        setDepts(prev => [...prev, data]);
        success(`Đã thêm bộ môn "${name}"`);
        setShowAdd(false);
      } else {
        showError('Không thể tạo bộ môn');
      }
    } catch {
      showError('Lỗi kết nối');
    }
  };

  return (
    <div style={{ padding: '12px 20px 12px 64px' }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
        <span style={{ fontSize: 12, fontWeight: 600, color: 'var(--gray-500)', textTransform: 'uppercase', letterSpacing: '0.06em' }}>
          Bộ môn
        </span>
        <button className="btn btn-secondary btn-sm" onClick={() => setShowAdd(true)} id={`btn-add-dept-${schoolId}`}>
          <Plus size={12} /> Thêm
        </button>
      </div>

      {loading ? (
        <div style={{ color: 'var(--gray-400)', fontSize: 13, padding: '8px 0' }}>Đang tải…</div>
      ) : depts.length === 0 ? (
        <div style={{ color: 'var(--gray-400)', fontSize: 13, padding: '8px 0', fontStyle: 'italic' }}>
          Chưa có bộ môn nào
        </div>
      ) : (
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
          {depts.map(d => (
            <div key={d.id} style={{
              display: 'flex', alignItems: 'center', gap: 6,
              padding: '5px 12px',
              background: 'white',
              border: '1px solid var(--gray-200)',
              borderRadius: 'var(--radius-full)',
              fontSize: 12.5,
              fontWeight: 500,
              color: 'var(--gray-700)',
            }}>
              {d.name}
            </div>
          ))}
        </div>
      )}

      {showAdd && (
        <InlineAddForm
          placeholder="Tên bộ môn..."
          onConfirm={addDept}
          onCancel={() => setShowAdd(false)}
        />
      )}
    </div>
  );
}

/* ============================================================
   INLINE ADD FORM
   ============================================================ */

function InlineAddForm({ placeholder, onConfirm, onCancel }: {
  placeholder: string;
  onConfirm: (name: string) => void;
  onCancel: () => void;
}) {
  const [value, setValue] = useState('');
  return (
    <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
      <input
        className="form-input"
        style={{ maxWidth: 280 }}
        placeholder={placeholder}
        value={value}
        onChange={e => setValue(e.target.value)}
        onKeyDown={e => { if (e.key === 'Enter' && value.trim()) onConfirm(value.trim()); if (e.key === 'Escape') onCancel(); }}
        autoFocus
      />
      <button className="btn btn-primary btn-sm" disabled={!value.trim()} onClick={() => onConfirm(value.trim())}>Thêm</button>
      <button className="btn btn-ghost btn-sm" onClick={onCancel}>Hủy</button>
    </div>
  );
}

/* ============================================================
   CREATE SCHOOL MODAL
   ============================================================ */

function CreateSchoolModal({ csrfToken, onClose, onCreated }: {
  csrfToken: string | null;
  onClose: () => void;
  onCreated: () => void;
}) {
  const [name, setName] = useState('');
  const [loading, setLoading] = useState(false);
  const [errMsg, setErrMsg] = useState('');
  const { error: showError } = useToast();

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || name.length > 200) { setErrMsg('Tên không hợp lệ (tối đa 200 ký tự)'); return; }
    if (!csrfToken) { showError('Thiếu CSRF token'); return; }

    setLoading(true);
    try {
      const res = await fetch('/api/v1/admin/schools', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken },
        body: JSON.stringify({ name: name.trim() }),
      });
      if (res.status === 201) {
        onCreated();
      } else if (res.status === 409) {
        setErrMsg('Trường này đã tồn tại trong hệ thống');
      } else {
        setErrMsg('Không thể tạo trường. Vui lòng thử lại');
      }
    } catch {
      setErrMsg('Lỗi kết nối máy chủ');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="modal-overlay" onClick={e => e.target === e.currentTarget && onClose()}>
      <div className="modal">
        <div className="modal-header">
          <div>
            <div className="modal-title">Thêm trường mới</div>
            <div className="modal-subtitle">Tạo một trường học trong hệ thống</div>
          </div>
          <button className="modal-close" onClick={onClose} aria-label="Close" id="btn-close-create-school">
            ✕
          </button>
        </div>
        <form onSubmit={submit}>
          <div className="modal-body">
            <div className="form-group">
              <label className="form-label" htmlFor="school-name">
                Tên trường <span className="required">*</span>
              </label>
              <input
                id="school-name"
                className={clsx('form-input', errMsg && 'error')}
                placeholder="VD: Trường THPT Nguyễn Du"
                value={name}
                onChange={e => { setName(e.target.value); setErrMsg(''); }}
                maxLength={200}
                autoFocus
              />
              {errMsg && <div className="form-error">⚠ {errMsg}</div>}
              <div className="form-hint">{name.length}/200 ký tự</div>
            </div>
          </div>
          <div className="modal-footer">
            <button type="button" className="btn btn-secondary" onClick={onClose} id="btn-cancel-school">Hủy</button>
            <button
              type="submit"
              className={clsx('btn btn-primary', loading && 'btn-loading')}
              disabled={loading || !name.trim()}
              id="btn-submit-school"
            >
              {loading ? 'Đang tạo…' : 'Tạo trường'}
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

function SkeletonTable({ rows, cols }: { rows: number; cols: number }) {
  return (
    <table>
      <thead>
        <tr>
          {Array.from({ length: cols }).map((_, i) => (
            <th key={i}><div className="skeleton skeleton-text" style={{ width: 80 }} /></th>
          ))}
        </tr>
      </thead>
      <tbody>
        {Array.from({ length: rows }).map((_, r) => (
          <tr key={r}>
            {Array.from({ length: cols }).map((_, c) => (
              <td key={c}><div className="skeleton skeleton-text" style={{ width: c === 0 ? 200 : 100 }} /></td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  );
}
