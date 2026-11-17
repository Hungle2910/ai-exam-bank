'use client';

import React, { useEffect, useState, useCallback, useMemo } from 'react';
import { Shield, Search, RefreshCw, Download, UserPlus, UserMinus } from 'lucide-react';
import clsx from 'clsx';
import { formatDateTime } from '@/lib/fetchOrMock';

/* Role assignment audit records returned by the authenticated API. */

type AuditAction = 'ROLE_GRANTED' | 'ROLE_REVOKED';

interface AuditEntry {
  id: string;
  occurredAt: string;
  actor: string;
  action: AuditAction;
  target: string;
  detail: string;
  schoolName: string;
}

const ACTION_META: Record<AuditAction, { label: string; badge: string; icon: React.ReactNode }> = {
  ROLE_GRANTED: { label: 'Cấp quyền', badge: 'badge-blue', icon: <UserPlus size={13} /> },
  ROLE_REVOKED: { label: 'Thu hồi quyền', badge: 'badge-amber', icon: <UserMinus size={13} /> },
};

/* ============================================================
   PAGE
   ============================================================ */

function isAuditEntry(value: unknown): value is AuditEntry {
  if (typeof value !== 'object' || value === null) return false;
  const row = value as Record<string, unknown>;
  return typeof row.id === 'string' && typeof row.occurredAt === 'string' &&
    typeof row.actor === 'string' && typeof row.target === 'string' &&
    typeof row.detail === 'string' && typeof row.schoolName === 'string' &&
    (row.action === 'ROLE_GRANTED' || row.action === 'ROLE_REVOKED');
}

export default function AuditPage() {
  const [entries, setEntries] = useState<AuditEntry[]>([]);
  const [loadError, setLoadError] = useState(false);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [action, setAction] = useState<'' | AuditAction>('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');

  const load = useCallback(() => {
    void fetch('/api/v1/audit/role-assignments?pageSize=100', { credentials: 'include' })
      .then(async response => {
        if (!response.ok) throw new Error('Audit API failed');
        const data: unknown = await response.json();
        if (!Array.isArray(data) || !data.every(isAuditEntry)) throw new Error('Invalid audit response');
        setEntries(data);
        setLoadError(false);
      })
      .catch(() => {
        setEntries([]);
        setLoadError(true);
      })
      .finally(() => setLoading(false));
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
      .map(v => {
        const safe = /^[\s\u0000-\u001f]*[=+\-@]/.test(v) ? `'${v}` : v;
        return `"${safe.replace(/"/g, '""')}"`;
      }).join(','));
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
        <p className="page-subtitle">Lịch sử phân quyền đã lưu trong hệ thống (100 bản ghi mới nhất)</p>
        <div className="page-actions">
          {loadError && <span className="badge badge-amber">Không tải được nhật ký; hãy thử lại</span>}
          <button className="btn btn-secondary" onClick={() => { setLoading(true); load(); }} id="btn-refresh-audit"><RefreshCw size={14} /></button>
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
