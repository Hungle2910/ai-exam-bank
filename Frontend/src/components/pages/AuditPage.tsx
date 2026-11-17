'use client';

import React, { useEffect, useState, useCallback, useMemo } from 'react';
<<<<<<< HEAD
import {
  Shield, Search, RefreshCw, Download, UserPlus, UserMinus,
  CheckCircle2, XCircle, LogIn, FileText, Upload, Cpu, Database
} from 'lucide-react';
import clsx from 'clsx';
import { formatDateTime } from '@/lib/fetchOrMock';
import { useWorkflowStore, LocalAuditItem } from '@/lib/workflowStore';

type AuditAction = LocalAuditItem['action'];
=======
import { Shield, Search, RefreshCw, Download, UserPlus, UserMinus } from 'lucide-react';
import clsx from 'clsx';
import { formatDateTime } from '@/lib/fetchOrMock';

/* Role assignment audit records returned by the authenticated API. */

type AuditAction = 'ROLE_GRANTED' | 'ROLE_REVOKED';
>>>>>>> origin/dev

interface AuditEntry {
  id: string;
  occurredAt: string;
  actor: string;
  action: AuditAction;
  target: string;
  detail: string;
  schoolName: string;
  correlationId?: string;
}

<<<<<<< HEAD
const ACTION_META: Record<
  AuditAction,
  { label: string; badge: string; icon: React.ReactNode }
> = {
  ROLE_GRANTED: { label: 'Cấp quyền', badge: 'badge-blue', icon: <UserPlus size={13} /> },
  ROLE_REVOKED: { label: 'Thu hồi quyền', badge: 'badge-amber', icon: <UserMinus size={13} /> },
  QUESTION_APPROVED: { label: 'Duyệt câu hỏi', badge: 'badge-green', icon: <CheckCircle2 size={13} /> },
  QUESTION_REJECTED: { label: 'Trả lại câu hỏi', badge: 'badge-red', icon: <XCircle size={13} /> },
  LOGIN: { label: 'Đăng nhập', badge: 'badge-gray', icon: <LogIn size={13} /> },
  QUESTION_CREATED: { label: 'Tạo câu hỏi', badge: 'badge-gray', icon: <FileText size={13} /> },
  QUESTION_EDITED: { label: 'Tạo revision mới', badge: 'badge-blue', icon: <FileText size={13} /> },
  IMPORT_COMMITTED: { label: 'Nhập CSV', badge: 'badge-teal', icon: <Upload size={13} /> },
  REVIEW_SUBMITTED: { label: 'Gửi duyệt', badge: 'badge-blue', icon: <FileText size={13} /> },
  DRAFT_GENERATED: { label: 'Gợi ý AI RAG', badge: 'badge-blue', icon: <Cpu size={13} /> },
  EXAM_FINALIZED: { label: 'Chốt đề bất biến', badge: 'badge-green', icon: <CheckCircle2 size={13} /> },
  EXAM_SELECTED: { label: 'Chọn đề kỳ thi', badge: 'badge-green', icon: <CheckCircle2 size={13} /> },
  EXAM_RETURNED: { label: 'Trả lại đề thi', badge: 'badge-red', icon: <XCircle size={13} /> },
=======
const ACTION_META: Record<AuditAction, { label: string; badge: string; icon: React.ReactNode }> = {
  ROLE_GRANTED: { label: 'Cấp quyền', badge: 'badge-blue', icon: <UserPlus size={13} /> },
  ROLE_REVOKED: { label: 'Thu hồi quyền', badge: 'badge-amber', icon: <UserMinus size={13} /> },
>>>>>>> origin/dev
};

function isAuditEntry(value: unknown): value is AuditEntry {
  if (typeof value !== 'object' || value === null) return false;
  const entry = value as Record<string, unknown>;
  return (
    typeof entry.id === 'string' &&
    entry.id.length > 0 &&
    typeof entry.occurredAt === 'string' &&
    typeof entry.actor === 'string' &&
    typeof entry.action === 'string' &&
    entry.action in ACTION_META &&
    typeof entry.target === 'string' &&
    typeof entry.detail === 'string' &&
    typeof entry.schoolName === 'string'
  );
}

function isAuditEntry(value: unknown): value is AuditEntry {
  if (typeof value !== 'object' || value === null) return false;
  const row = value as Record<string, unknown>;
  return typeof row.id === 'string' && typeof row.occurredAt === 'string' &&
    typeof row.actor === 'string' && typeof row.target === 'string' &&
    typeof row.detail === 'string' && typeof row.schoolName === 'string' &&
    (row.action === 'ROLE_GRANTED' || row.action === 'ROLE_REVOKED');
}

export default function AuditPage() {
<<<<<<< HEAD
  const wf = useWorkflowStore();
  const [apiEntries, setApiEntries] = useState<AuditEntry[]>([]);
=======
  const [entries, setEntries] = useState<AuditEntry[]>([]);
>>>>>>> origin/dev
  const [loadError, setLoadError] = useState(false);
  const [loading, setLoading] = useState(true);
  const [sandboxMode, setSandboxMode] = useState(false);

  const [search, setSearch] = useState('');
  const [action, setAction] = useState<'' | AuditAction>('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');

<<<<<<< HEAD
  const fetchAudit = useCallback(async () => {
    try {
      const response = await fetch('/api/v1/audit/role-assignments?pageSize=100', { credentials: 'include' });
      if (!response.ok) throw new Error(`Audit API returned ${response.status}`);
      const data: unknown = await response.json();
      if (!Array.isArray(data) || !data.every(isAuditEntry)) {
        throw new Error('Invalid audit response');
      }
      setApiEntries(data);
      setLoadError(false);
      setSandboxMode(false);
    } catch {
      setApiEntries([]);
      setLoadError(true);
    } finally {
      setLoading(false);
    }
=======
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
>>>>>>> origin/dev
  }, []);

  useEffect(() => {
    let active = true;
    const run = async () => {
      await fetchAudit();
      if (!active) return;
    };
    void run();
    return () => {
      active = false;
    };
  }, [fetchAudit]);

  const entries: AuditEntry[] = sandboxMode ? wf.auditTrail : apiEntries;

  const filtered = useMemo(
    () =>
      entries.filter(e => {
        const q = search.toLowerCase();
        if (q && !`${e.actor} ${e.target} ${e.detail}`.toLowerCase().includes(q)) return false;
        if (action && e.action !== action) return false;
        const d = e.occurredAt.slice(0, 10);
        if (from && d < from) return false;
        if (to && d > to) return false;
        return true;
      }),
    [entries, search, action, from, to],
  );

  const exportCsv = () => {
    if (sandboxMode || filtered.length === 0) return;
    const header = 'Thời gian,Tác nhân,Hành động,Đối tượng,Chi tiết,Trường';
<<<<<<< HEAD
    const rows = filtered.map(e =>
      [
        formatDateTime(e.occurredAt),
        e.actor,
        ACTION_META[e.action].label,
        e.target,
        e.detail,
        e.schoolName,
      ]
        .map(v => {
          const safe = /^[\s\u0000-\u001f]*[=+\-@]/.test(v) ? `'${v}` : v;
          return `"${safe.replace(/"/g, '""')}"`;
        })
        .join(','),
    );
    const blob = new Blob(['\uFEFF' + [header, ...rows].join('\n')], {
      type: 'text/csv;charset=utf-8',
    });
=======
    const rows = filtered.map(e => [formatDateTime(e.occurredAt), e.actor, ACTION_META[e.action].label, e.target, e.detail, e.schoolName]
      .map(v => {
        const safe = /^[\s\u0000-\u001f]*[=+\-@]/.test(v) ? `'${v}` : v;
        return `"${safe.replace(/"/g, '""')}"`;
      }).join(','));
    const blob = new Blob(['\uFEFF' + [header, ...rows].join('\n')], { type: 'text/csv;charset=utf-8' });
>>>>>>> origin/dev
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `audit-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(a.href);
  };

  return (
    <div>
      <div className="page-header">
<<<<<<< HEAD
        <div>
          <h1 className="page-title">Audit Log</h1>
          <p className="page-subtitle">
            Lịch sử bất biến các thao tác phân quyền, duyệt câu hỏi, nhập CSV và chốt đề (M3-05)
          </p>
        </div>
        <div className="page-actions">
          {sandboxMode && (
            <span className="badge badge-amber">
              <Database size={12} />
              Nhật ký phiên cục bộ (Không cho phép xuất CSV mẫu)
            </span>
          )}
          <button
            className="btn btn-secondary"
            onClick={() => {
              setLoading(true);
              void fetchAudit();
            }}
            id="btn-refresh-audit"
          >
            <RefreshCw size={14} />
          </button>
          <button
            className="btn btn-primary"
            onClick={exportCsv}
            disabled={sandboxMode || filtered.length === 0}
            id="btn-export-audit"
          >
            <Download size={14} />
            Xuất CSV
=======
        <h1 className="page-title">Audit Log</h1>
        <p className="page-subtitle">Lịch sử phân quyền đã lưu trong hệ thống (100 bản ghi mới nhất)</p>
        <div className="page-actions">
          {loadError && <span className="badge badge-amber">Không tải được nhật ký; hãy thử lại</span>}
          <button className="btn btn-secondary" onClick={() => { setLoading(true); load(); }} id="btn-refresh-audit"><RefreshCw size={14} /></button>
          <button className="btn btn-primary" onClick={exportCsv} disabled={filtered.length === 0} id="btn-export-audit">
            <Download size={14} />Xuất CSV
>>>>>>> origin/dev
          </button>
        </div>
      </div>

      <div className="table-container">
        <div className="table-toolbar" style={{ flexWrap: 'wrap', gap: 10 }}>
          <div className="search-bar" style={{ width: 260 }}>
            <Search size={14} className="search-bar-icon" />
            <input
              placeholder="Tác nhân, đối tượng, chi tiết…"
              value={search}
              onChange={e => setSearch(e.target.value)}
              id="audit-search"
            />
          </div>
          <select
            className="form-select"
            style={{ width: 185 }}
            value={action}
            onChange={e => setAction(e.target.value as AuditAction | '')}
            id="audit-action-filter"
            aria-label="Lọc hành động"
          >
            <option value="">Tất cả hành động</option>
            {(Object.keys(ACTION_META) as AuditAction[]).map(a => (
              <option key={a} value={a}>
                {ACTION_META[a].label}
              </option>
            ))}
          </select>
          <input
            type="date"
            className="form-input"
            style={{ width: 150 }}
            value={from}
            onChange={e => setFrom(e.target.value)}
            id="audit-from"
            aria-label="Từ ngày"
          />
          <span style={{ color: 'var(--gray-400)' }}>→</span>
          <input
            type="date"
            className="form-input"
            style={{ width: 150 }}
            value={to}
            onChange={e => setTo(e.target.value)}
            id="audit-to"
            aria-label="Đến ngày"
          />
          <span style={{ marginLeft: 'auto', fontSize: 12.5, color: 'var(--gray-400)' }}>
            {filtered.length} bản ghi
          </span>
        </div>

        {loading ? (
          <div style={{ padding: 16 }}>
            {[0, 1, 2, 3].map(i => (
              <div
                key={i}
                className="skeleton"
                style={{ height: 40, marginBottom: 8, borderRadius: 6 }}
              />
            ))}
          </div>
        ) : loadError && !sandboxMode ? (
          <div className="table-empty" style={{ padding: 28 }}>
            <div className="table-empty-text">
              Không tải được lịch sử thao tác. Vui lòng thử lại.
            </div>
            <div className="table-empty-sub" style={{ marginTop: 6 }}>
              Endpoint <code>/api/v1/audit</code> hiện trả lỗi hoặc chưa triển khai (Fail-closed theo M3-05, khóa xuất CSV mẫu).
            </div>
            <button
              type="button"
              className="btn btn-secondary btn-sm"
              style={{ marginTop: 12 }}
              onClick={() => setSandboxMode(true)}
              id="btn-enable-audit-sandbox"
            >
              <Database size={13} />
              Xem nhật ký thao tác trong phiên cục bộ ({wf.auditTrail.length} sự kiện)
            </button>
          </div>
        ) : filtered.length === 0 ? (
          <div className="table-empty">
            <div className="table-empty-icon">
              <Shield />
            </div>
            <div className="table-empty-text">Không có bản ghi phù hợp</div>
            <div className="table-empty-sub">Thử thay đổi bộ lọc</div>
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table>
              <thead>
                <tr>
                  <th>Thời gian</th>
                  <th>Tác nhân</th>
                  <th>Hành động</th>
                  <th>Đối tượng</th>
                  <th>Chi tiết</th>
                  <th>Trường / Phạm vi</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map(e => {
                  const m = ACTION_META[e.action];
                  return (
                    <tr key={e.id}>
                      <td
                        style={{
                          whiteSpace: 'nowrap',
                          fontVariantNumeric: 'tabular-nums',
                          color: 'var(--gray-500)',
                        }}
                      >
                        {formatDateTime(e.occurredAt)}
                      </td>
                      <td style={{ fontWeight: 600, color: 'var(--gray-900)' }}>{e.actor}</td>
                      <td>
                        <span className={clsx('badge', m.badge)}>
                          {m.icon}
                          {m.label}
                        </span>
                      </td>
                      <td>
                        <code
                          style={{
                            fontSize: 12,
                            background: 'var(--gray-100)',
                            padding: '2px 6px',
                            borderRadius: 4,
                          }}
                        >
                          {e.target}
                        </code>
                      </td>
                      <td style={{ color: 'var(--gray-600)' }}>{e.detail}</td>
                      <td style={{ color: 'var(--gray-500)' }}>{e.schoolName}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
