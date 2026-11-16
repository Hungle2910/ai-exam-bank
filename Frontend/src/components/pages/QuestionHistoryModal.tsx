'use client';

import React, { useEffect, useMemo, useState } from 'react';
import clsx from 'clsx';
import { useAuth } from '@/contexts/AuthContext';
import { ApiError, formatDateTime } from '@/lib/fetchOrMock';
import { compareRevisions } from './revisionDiff';
import { DIFF, ORIGIN, STATUS, type QuestionHistory, type RevisionDetail } from './questionLabels';

const LABELS = 'ABCD';

const describeError = (e: unknown) =>
  e instanceof ApiError && e.status === 401 ? 'Phiên đăng nhập đã hết hạn, hãy đăng nhập lại'
    : e instanceof ApiError && e.status === 403 ? 'Bạn không có quyền xem lịch sử câu hỏi này'
      : e instanceof ApiError && e.status === 404 ? 'Không tìm thấy câu hỏi'
        : e instanceof ApiError && e.status === 0 ? 'Không kết nối được máy chủ'
          : 'Không thể tải lịch sử revision';

/** Loads GET /questions/{id}/revisions, or builds the same shape from the sample data in development. */
function useHistory(id: string, mockRevisions: RevisionDetail[] | null) {
  const { apiFetch } = useAuth();
  const [tick, setTick] = useState(0);
  const [result, setResult] = useState<{ key: string; history: QuestionHistory | null; error: string | null } | null>(null);
  const isMock = mockRevisions !== null;
  const key = `${id}#${isMock}#${tick}`;

  useEffect(() => {
    let cancelled = false;
    const load = async (): Promise<QuestionHistory> => {
      if (mockRevisions) {
        const revisions = [...mockRevisions].sort((a, b) => b.revision - a.revision);
        const latest = revisions[0]?.revision ?? 0;
        const approved = revisions.find(r => r.status === 'APPROVED')?.revision ?? null;
        return {
          id, subject: { id: 'mock-subject', name: '' }, latestRevision: latest, approvedRevision: approved,
          revisions: revisions.map(r => ({ ...r, isLatest: r.revision === latest, isApprovedInUse: r.revision === approved })),
        };
      }
      const res = await apiFetch(`/api/v1/questions/${id}/revisions`, { credentials: 'include' });
      if (!res.ok) throw new ApiError(res.status, `API lỗi ${res.status}`);
      return await res.json() as QuestionHistory;
    };
    load().then(
      history => { if (!cancelled) setResult({ key, history, error: null }); },
      e => { if (!cancelled) setResult({ key, history: null, error: describeError(e) }); },
    );
    return () => { cancelled = true; };
    // mockRevisions is read once per open: edits to the sample data must not reshuffle a history being read.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, key, apiFetch]);

  const settled = result?.key === key ? result : null;
  return { history: settled?.history ?? null, error: settled?.error ?? null, loading: !settled, reload: () => setTick(t => t + 1) };
}

export default function QuestionHistoryModal({ id, mockRevisions, onClose, onSubmitReview }: {
  id: string; mockRevisions: RevisionDetail[] | null; onClose: () => void;
  /** Present only when the caller may submit; resolves true when the server accepted it. */
  onSubmitReview?: (revision: number) => Promise<boolean>;
}) {
  const { history, error, loading, reload } = useHistory(id, mockRevisions);

  return (
    <div className="modal-overlay" onClick={e => e.target === e.currentTarget && onClose()}>
      <div className="modal modal-lg" role="dialog" aria-label="Lịch sử revision">
        <div className="modal-header">
          <div><div className="modal-title">Lịch sử revision</div><div className="modal-subtitle">{id} · mỗi revision là bất biến; sửa câu hỏi luôn tạo revision mới</div></div>
          <button className="modal-close" onClick={onClose} id="btn-close-history" aria-label="Đóng">✕</button>
        </div>
        <div className="modal-body">
          {loading && <div className="skeleton" style={{ height: 160, borderRadius: 8 }} />}
          {error && <div role="alert">⚠ {error} <button className="btn btn-secondary btn-sm" onClick={reload} id="btn-retry-history">Thử lại</button></div>}
          {history && (history.revisions.length === 0
            ? <div>Câu hỏi chưa có revision nào.</div>
            : <HistoryBody history={history} onSubmitReview={onSubmitReview} onSubmitted={reload} />)}
        </div>
      </div>
    </div>
  );
}

function HistoryBody({ history, onSubmitReview, onSubmitted }: {
  history: QuestionHistory; onSubmitReview?: (revision: number) => Promise<boolean>; onSubmitted: () => void;
}) {
  const { revisions } = history;
  const previousOf = (revision: number) => revisions.find(r => r.revision < revision)?.revision ?? null; // list is newest first
  const [pick, setPick] = useState<{ revision: number; compare: number | null } | null>(null);

  const viewed = pick?.revision ?? history.latestRevision;
  const compare = pick ? pick.compare : previousOf(viewed);
  const current = revisions.find(r => r.revision === viewed) ?? revisions[0];
  const other = compare === null ? undefined : revisions.find(r => r.revision === compare);

  const choose = (revision: number) => setPick({ revision, compare: previousOf(revision) });

  // Only the newest revision can be submitted, and only while it is still a draft.
  const [submitting, setSubmitting] = useState(false);
  const canSubmit = !!onSubmitReview && current.isLatest && current.status === 'DRAFT';
  const submit = async () => {
    if (!onSubmitReview) return;
    setSubmitting(true);
    try { if (await onSubmitReview(current.revision)) onSubmitted(); } finally { setSubmitting(false); }
  };

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '210px 1fr', gap: 18, alignItems: 'start' }}>
      <ol aria-label="Các revision" style={{ listStyle: 'none', margin: 0, padding: 0, display: 'grid', gap: 6 }}>
        {revisions.map(r => (
          <li key={r.revision}>
            <button type="button" id={`btn-revision-${r.revision}`} onClick={() => choose(r.revision)} aria-current={r.revision === current.revision}
              style={{ width: '100%', textAlign: 'left', padding: '8px 10px', borderRadius: 8, cursor: 'pointer',
                border: `1px solid ${r.revision === current.revision ? 'var(--brand-600)' : 'var(--gray-200)'}`,
                background: r.revision === current.revision ? 'var(--brand-50)' : 'white' }}>
              <div style={{ fontWeight: 600, fontSize: 13.5 }}>Revision {r.revision}</div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4, margin: '4px 0' }}>
                <span className={clsx('badge', STATUS[r.status][1])}>{STATUS[r.status][0]}</span>
                {r.isLatest && <span className="badge badge-blue">Mới nhất</span>}
                {r.isApprovedInUse && <span className="badge badge-green">Đang dùng để thi</span>}
              </div>
              <div style={{ fontSize: 12, color: 'var(--gray-500)' }}>{ORIGIN[r.origin]} · {formatDateTime(r.createdAt)}</div>
            </button>
          </li>
        ))}
      </ol>

      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 12, flexWrap: 'wrap' }}>
          <div style={{ fontWeight: 600 }}>Revision {current.revision}</div>
          {canSubmit && (
            <button type="button" className={clsx('btn btn-primary btn-sm', submitting && 'btn-loading')} disabled={submitting} onClick={submit} id="btn-history-submit-review">Gửi duyệt</button>
          )}
          {revisions.length > 1 && (
            <label style={{ marginLeft: 'auto', fontSize: 12.5, color: 'var(--gray-500)' }}>
              So sánh với{' '}
              <select className="form-select" style={{ width: 170, display: 'inline-block' }} id="select-compare" value={compare ?? ''}
                onChange={e => setPick({ revision: current.revision, compare: e.target.value ? Number(e.target.value) : null })}>
                <option value="">Không so sánh</option>
                {revisions.filter(r => r.revision !== current.revision).map(r => <option key={r.revision} value={r.revision}>Revision {r.revision}</option>)}
              </select>
            </label>
          )}
        </div>
        {other ? <CompareView a={other} b={current} /> : <RevisionView revision={current} />}
      </div>
    </div>
  );
}

function RevisionView({ revision: r }: { revision: RevisionDetail }) {
  return (
    <div id="revision-view">
      <p style={{ fontSize: 14, lineHeight: 1.5, marginTop: 0 }}>{r.stem}</p>
      <ul style={{ listStyle: 'none', padding: 0, margin: '0 0 12px', display: 'grid', gap: 6 }}>
        {r.options.map((o, i) => (
          <li key={i} style={{ display: 'flex', gap: 8, fontWeight: LABELS[i] === r.correctOption ? 600 : 400 }}>
            <span>{LABELS[i]}.</span><span>{o}</span>
            {LABELS[i] === r.correctOption && <span className="badge badge-green">Đáp án đúng</span>}
          </li>
        ))}
      </ul>
      <dl style={{ display: 'grid', gridTemplateColumns: 'max-content 1fr', gap: '4px 12px', fontSize: 12.5, color: 'var(--gray-600)', margin: 0 }}>
        <dt>Chủ đề</dt><dd style={{ margin: 0 }}>{r.topic.name}</dd>
        <dt>Độ khó</dt><dd style={{ margin: 0 }}>{DIFF[r.difficulty][0]}</dd>
        <dt>Nguồn</dt><dd style={{ margin: 0 }}>{r.source ?? '—'}</dd>
        {r.importBatchId && <><dt>Lô import</dt><dd style={{ margin: 0 }}>{r.importBatchId} · dòng {r.importRowNumber ?? '?'}</dd></>}
      </dl>
    </div>
  );
}

function CompareView({ a, b }: { a: RevisionDetail; b: RevisionDetail }) {
  // Always older on the left, newer on the right, whichever one the user picked first.
  const [older, newer] = a.revision < b.revision ? [a, b] : [b, a];
  const diffs = useMemo(() => compareRevisions(older, newer), [older, newer]);
  const changed = diffs.filter(d => d.changed).length;
  const cell = (changedField: boolean, tone: 'old' | 'new'): React.CSSProperties => ({
    padding: '6px 8px', borderRadius: 6, fontSize: 13, whiteSpace: 'pre-wrap', overflowWrap: 'anywhere',
    background: !changedField ? 'transparent' : tone === 'old' ? 'var(--danger-50)' : 'var(--accent-50, #ecfdf5)',
  });

  return (
    <div id="revision-compare">
      <div role="status" style={{ fontSize: 12.5, color: 'var(--gray-500)', marginBottom: 8 }}>
        {changed === 0 ? 'Hai revision có nội dung giống nhau.' : `${changed} trường khác nhau`}
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '110px 1fr 1fr', gap: 4, fontSize: 12, fontWeight: 600, color: 'var(--gray-500)', marginBottom: 4 }}>
        <span /><span>Revision {older.revision}</span><span>Revision {newer.revision}</span>
      </div>
      {diffs.map(d => (
        <div key={d.key} data-changed={d.changed} style={{ display: 'grid', gridTemplateColumns: '110px 1fr 1fr', gap: 4, marginBottom: 4, alignItems: 'start' }}>
          <span style={{ fontSize: 12.5, color: 'var(--gray-500)', paddingTop: 6 }}>{d.label}</span>
          <div style={cell(d.changed, 'old')}>{d.before}</div>
          <div style={cell(d.changed, 'new')}>{d.after}</div>
        </div>
      ))}
    </div>
  );
}
