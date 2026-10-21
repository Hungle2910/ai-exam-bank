'use client';

import React, { useEffect, useState, useCallback, useMemo } from 'react';
import { Shield, Search, RefreshCw, Download, Database, UserPlus, UserMinus, CheckCircle2, XCircle, LogIn } from 'lucide-react';
import clsx from 'clsx';
import { fetchOrMock, formatDateTime } from '@/lib/fetchOrMock';

/* ============================================================
   TYPES & MOCK
   ============================================================ */

type AuditAction = 'ROLE_GRANTED' | 'ROLE_REVOKED' | 'QUESTION_APPROVED' | 'QUESTION_REJECTED' | 'LOGIN';

interface AuditEntry {
  id: string;
  occurredAt: string;
  actor: string;
  action: AuditAction;
  target: string;
  detail: string;
  schoolName: string;
}

const MOCK: AuditEntry[] = [
  { id: 'a1', occurredAt: '2026-10-17T03:42:00Z', actor: 'admin.thpt.a', action: 'ROLE_GRANTED', target: 'gv.tran.thi.b', detail: 'Cấp vai trò Trưởng Bộ Môn (Toán)', schoolName: 'THPT Nguyễn Huệ' },
  { id: 'a2', occurredAt: '2026-10-17T02:15:00Z', actor: 'tbm.toan', action: 'QUESTION_APPROVED', target: 'q-1004 rev 1', detail: 'Duyệt câu hỏi Logarit', schoolName: 'THPT Nguyễn Huệ' },
  { id: 'a3', occurredAt: '2026-10-16T09:58:00Z', actor: 'tbm.van', action: 'QUESTION_REJECTED', target: 'q-1005 rev 1', detail: 'Lý do: Thiếu trích dẫn nguồn tài liệu', schoolName: 'THPT Lê Lợi' },
  { id: 'a4', occurredAt: '2026-10-16T08:01:00Z', actor: 'ministry.admin', action: 'ROLE_REVOKED', target: 'gv.cu.x', detail: 'Thu hồi vai trò Giáo viên', schoolName: 'THPT Lê Lợi' },
  { id: 'a5', occurredAt: '2026-10-16T07:30:00Z', actor: 'gv.nguyen.van.a', action: 'LOGIN', target: '—', detail: 'Đăng nhập thành công', schoolName: 'THPT Nguyễn Huệ' },
  { id: 'a6', occurredAt: '2026-10-15T15:12:00Z', actor: 'admin.thpt.a', action: 'ROLE_GRANTED', target: 'gv.le.van.c', detail: 'Cấp vai trò Giáo viên', schoolName: 'THPT Nguyễn Huệ' },
];

const ACTION_META: Record<AuditAction, { label: string; badge: string; icon: React.ReactNode }> = {
  ROLE_GRANTED: { label: 'Cấp quyền', badge: 'badge-blue', icon: <UserPlus size={13} /> },
  ROLE_REVOKED: { label: 'Thu hồi quyền', badge: 'badge-amber', icon: <UserMinus size={13} /> },
  QUESTION_APPROVED: { label: 'Duyệt', badge: 'badge-green', icon: <CheckCircle2 size={13} /> },
  QUESTION_REJECTED: { label: 'Trả lại', badge: 'badge-red', icon: <XCircle size={13} /> },
  LOGIN: { label: 'Đăng nhập', badge: 'badge-gray', icon: <LogIn size={13} /> },
};

/* ============================================================
   PAGE
   ============================================================ */

export default function AuditPage() {
  const [entries, setEntries] = useState<AuditEntry[]>([]);
  const [isMock, setIsMock] = useState(false);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [action, setAction] = useState<'' | AuditAction>('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    const r = await fetchOrMock<AuditEntry[]>('/api/v1/audit', MOCK);
    setEntries(r.data);
    setIsMock(r.isMock);
    setLoading(false);
  }, []);

  useEffect(() => { load(); }, [load]);

  const filtered = useMemo(() => entries.filter(e => {
    const q = search.toLowerCase();
    if (q && !`${e.actor} ${e.target} ${e.detail}`.toLowerCase().includes(q)) return false;
    if (action && e.action !== action) return false;
    const d = e.occurredAt.slice(0, 10);
    if (from && d < from) return false;
    if (to && d > to) return false;
    return true;
  }), [entries, search, action, from, to]);

  const exportCsv = () => {
    const header = 'Thời gian,Tác nhân,Hành động,Đối tượng,Chi tiết,Trường';
    const rows = filtered.map(e => [formatDateTime(e.occurredAt), e.actor, ACTION_META[e.action].label, e.target, e.detail, e.schoolName]
      .map(v => `"${v.replace(/"/g, '""')}"`).join(','));
    const blob = new Blob(['\uFEFF' + [header, ...rows].join('\n')], { type: 'text/csv;charset=utf-8' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `audit-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(a.href);
  };

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Audit Log</h1>
        <p className="page-subtitle">Lịch sử bất biến mọi thao tác phân quyền và duyệt câu hỏi</p>
        <div className="page-actions">
          {isMock && <span className="badge badge-amber"><Database size={12} />Dữ liệu mẫu</span>}
          <button className="btn btn-secondary" onClick={load} id="btn-refresh-audit"><RefreshCw size={14} /></button>
          <button className="btn btn-primary" onClick={exportCsv} disabled={filtered.length === 0} id="btn-export-audit">
            <Download size={14} />Xuất CSV
          </button>
        </div>
      </div>

      <div className="table-container">
        <div className="table-toolbar" style={{ flexWrap: 'wrap', gap: 10 }}>
          <div className="search-bar" style={{ width: 260 }}>
            <Search size={14} className="search-bar-icon" />
            <input placeholder="Tác nhân, đối tượng, chi tiết…" value={search} onChange={e => setSearch(e.target.value)} id="audit-search" />
          </div>
          <select className="form-select" style={{ width: 170 }} value={action} onChange={e => setAction(e.target.value as AuditAction | '')} id="audit-action-filter" aria-label="Lọc hành động">
            <option value="">Tất cả hành động</option>
            {(Object.keys(ACTION_META) as AuditAction[]).map(a => <option key={a} value={a}>{ACTION_META[a].label}</option>)}
          </select>
          <input type="date" className="form-input" style={{ width: 150 }} value={from} onChange={e => setFrom(e.target.value)} id="audit-from" aria-label="Từ ngày" />
          <span style={{ color: 'var(--gray-400)' }}>→</span>
          <input type="date" className="form-input" style={{ width: 150 }} value={to} onChange={e => setTo(e.target.value)} id="audit-to" aria-label="Đến ngày" />
          <span style={{ marginLeft: 'auto', fontSize: 12.5, color: 'var(--gray-400)' }}>{filtered.length} bản ghi</span>
        </div>

        {loading ? (
          <div style={{ padding: 16 }}>{[0, 1, 2, 3].map(i => <div key={i} className="skeleton" style={{ height: 40, marginBottom: 8, borderRadius: 6 }} />)}</div>
        ) : filtered.length === 0 ? (
          <div className="table-empty">
            <div className="table-empty-icon"><Shield /></div>
            <div className="table-empty-text">Không có bản ghi phù hợp</div>
            <div className="table-empty-sub">Thử thay đổi bộ lọc</div>
          </div>
        ) : (
          <table>
            <thead>
              <tr><th>Thời gian</th><th>Tác nhân</th><th>Hành động</th><th>Đối tượng</th><th>Chi tiết</th><th>Trường</th></tr>
            </thead>
            <tbody>
              {filtered.map(e => {
                const m = ACTION_META[e.action];
                return (
                  <tr key={e.id}>
                    <td style={{ whiteSpace: 'nowrap', fontVariantNumeric: 'tabular-nums', color: 'var(--gray-500)' }}>{formatDateTime(e.occurredAt)}</td>
                    <td style={{ fontWeight: 600, color: 'var(--gray-900)' }}>{e.actor}</td>
                    <td><span className={clsx('badge', m.badge)}>{m.icon}{m.label}</span></td>
                    <td><code style={{ fontSize: 12, background: 'var(--gray-100)', padding: '2px 6px', borderRadius: 4 }}>{e.target}</code></td>
                    <td style={{ color: 'var(--gray-600)' }}>{e.detail}</td>
                    <td style={{ color: 'var(--gray-500)' }}>{e.schoolName}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
