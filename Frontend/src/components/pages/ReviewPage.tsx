'use client';

import React, { useEffect, useState, useCallback } from 'react';
import { ClipboardCheck, Check, X, RefreshCw, Search, Clock, Database } from 'lucide-react';
import clsx from 'clsx';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
import { fetchOrMock, formatDateTime } from '@/lib/fetchOrMock';

/* ============================================================
   TYPES & MOCK
   ============================================================ */

type ReviewStatus = 'REVIEW_REQUIRED' | 'APPROVED' | 'REJECTED';
type Difficulty = 'EASY' | 'MEDIUM' | 'HARD';

interface ReviewItem {
  id: string;
  revision: number;
  content: string;
  options: string[];
  correctIndex: number;
  subject: string;
  topic: string;
  difficulty: Difficulty;
  points: number;
  author: string;
  submittedAt: string;
  status: ReviewStatus;
  source: 'MANUAL' | 'IMPORT' | 'AI';
  rejectReason?: string;
}

const MOCK: ReviewItem[] = [
  { id: 'q-1001', revision: 2, content: 'Đạo hàm của hàm số y = x³ − 3x + 2 là?', options: ['3x² − 3', '3x² + 3', 'x² − 3', '3x − 3'], correctIndex: 0, subject: 'Toán', topic: 'Đạo hàm', difficulty: 'EASY', points: 0.25, author: 'gv.nguyen.van.a', submittedAt: '2026-10-16T08:12:00Z', status: 'REVIEW_REQUIRED', source: 'MANUAL' },
  { id: 'q-1002', revision: 1, content: 'Nguyên tố nào có độ âm điện lớn nhất?', options: ['O', 'Cl', 'F', 'N'], correctIndex: 2, subject: 'Hóa học', topic: 'Bảng tuần hoàn', difficulty: 'MEDIUM', points: 0.25, author: 'gv.tran.thi.b', submittedAt: '2026-10-16T09:40:00Z', status: 'REVIEW_REQUIRED', source: 'AI' },
  { id: 'q-1003', revision: 3, content: 'Một vật dao động điều hòa với phương trình x = 5cos(4πt) cm. Chu kỳ dao động là?', options: ['0,25 s', '0,5 s', '2 s', '4 s'], correctIndex: 1, subject: 'Vật lý', topic: 'Dao động cơ', difficulty: 'MEDIUM', points: 0.25, author: 'gv.le.van.c', submittedAt: '2026-10-15T14:05:00Z', status: 'REVIEW_REQUIRED', source: 'IMPORT' },
  { id: 'q-1004', revision: 1, content: 'Số nghiệm thực của phương trình log₂(x² − 1) = 3 là?', options: ['0', '1', '2', '3'], correctIndex: 2, subject: 'Toán', topic: 'Logarit', difficulty: 'HARD', points: 0.5, author: 'gv.nguyen.van.a', submittedAt: '2026-10-15T10:22:00Z', status: 'APPROVED', source: 'MANUAL' },
  { id: 'q-1005', revision: 1, content: 'Tác phẩm "Vợ chồng A Phủ" của tác giả nào?', options: ['Kim Lân', 'Tô Hoài', 'Nam Cao', 'Nguyễn Tuân'], correctIndex: 1, subject: 'Ngữ văn', topic: 'Văn học hiện đại', difficulty: 'EASY', points: 0.25, author: 'gv.pham.d', submittedAt: '2026-10-14T16:48:00Z', status: 'REJECTED', source: 'AI', rejectReason: 'Thiếu trích dẫn nguồn tài liệu' },
];

const DIFF_LABEL: Record<Difficulty, [string, string]> = {
  EASY: ['Dễ', 'badge-green'], MEDIUM: ['Trung bình', 'badge-amber'], HARD: ['Khó', 'badge-red'],
};
const SOURCE_LABEL = { MANUAL: ['Thủ công', 'badge-gray'], IMPORT: ['CSV', 'badge-teal'], AI: ['AI Draft', 'badge-purple'] } as const;
const STATUS_TABS: { key: ReviewStatus; label: string }[] = [
  { key: 'REVIEW_REQUIRED', label: 'Chờ duyệt' },
  { key: 'APPROVED', label: 'Đã duyệt' },
  { key: 'REJECTED', label: 'Đã trả lại' },
];

/* ============================================================
   PAGE
   ============================================================ */

export default function ReviewPage() {
  const { csrfToken } = useAuth();
  const { success, error: showError } = useToast();
  const [items, setItems] = useState<ReviewItem[]>([]);
  const [isMock, setIsMock] = useState(false);
  const [loading, setLoading] = useState(true);
  const [tab, setTab] = useState<ReviewStatus>('REVIEW_REQUIRED');
  const [search, setSearch] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [rejecting, setRejecting] = useState(false);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    const r = await fetchOrMock<ReviewItem[]>('/api/v1/reviews', MOCK);
    setItems(r.data);
    setIsMock(r.isMock);
    setLoading(false);
  }, []);

  useEffect(() => { load(); }, [load]);

  const visible = items.filter(i =>
    i.status === tab &&
    (i.content.toLowerCase().includes(search.toLowerCase()) || i.subject.toLowerCase().includes(search.toLowerCase())),
  );
  const selected = items.find(i => i.id === selectedId) ?? visible[0] ?? null;

  const decide = async (approve: boolean) => {
    if (!selected) return;
    if (!approve && !reason.trim()) { showError('Vui lòng nhập lý do trả lại'); return; }
    setBusy(true);
    try {
      if (!isMock) {
        const res = await fetch(`/api/v1/reviews/${selected.id}/${approve ? 'approve' : 'reject'}`, {
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json', ...(csrfToken ? { 'X-CSRF-TOKEN': csrfToken } : {}) },
          body: JSON.stringify({ revision: selected.revision, reason: approve ? undefined : reason.trim() }),
        });
        if (res.status === 409) { showError('Câu hỏi đã được người khác xử lý. Đang tải lại…'); await load(); return; }
        if (!res.ok) { showError('Không thể lưu quyết định'); return; }
      }
      setItems(prev => prev.map(i => i.id === selected.id
        ? { ...i, status: approve ? 'APPROVED' : 'REJECTED', rejectReason: approve ? undefined : reason.trim() }
        : i));
      success(approve ? 'Đã duyệt câu hỏi' : 'Đã trả lại câu hỏi');
      setRejecting(false); setReason(''); setSelectedId(null);
    } catch {
      showError('Lỗi kết nối');
    } finally {
      setBusy(false);
    }
  };

  const countOf = (s: ReviewStatus) => items.filter(i => i.status === s).length;

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Chờ duyệt</h1>
        <p className="page-subtitle">Xem xét, phê duyệt hoặc trả lại các revision câu hỏi</p>
        <div className="page-actions">
          {isMock && <span className="badge badge-amber"><Database size={12} />Dữ liệu mẫu</span>}
          <button className="btn btn-secondary" onClick={load} id="btn-refresh-review"><RefreshCw size={14} /></button>
        </div>
      </div>

      <div className="tabs">
        {STATUS_TABS.map(t => (
          <div key={t.key} className={clsx('tab-item', tab === t.key && 'active')}
            onClick={() => { setTab(t.key); setSelectedId(null); setRejecting(false); }} id={`tab-review-${t.key}`}>
            {t.label}<span className="tab-count">{countOf(t.key)}</span>
          </div>
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(300px, 380px) 1fr', gap: 20, alignItems: 'start' }} className="review-grid">
        {/* List */}
        <div className="table-container">
          <div className="table-toolbar">
            <div className="search-bar" style={{ width: '100%' }}>
              <Search size={14} className="search-bar-icon" />
              <input placeholder="Tìm nội dung, môn học…" value={search} onChange={e => setSearch(e.target.value)} id="review-search" />
            </div>
          </div>
          {loading ? (
            <div style={{ padding: 16 }}>{[0, 1, 2].map(i => <div key={i} className="skeleton" style={{ height: 64, marginBottom: 10, borderRadius: 8 }} />)}</div>
          ) : visible.length === 0 ? (
            <div className="table-empty">
              <div className="table-empty-icon"><ClipboardCheck /></div>
              <div className="table-empty-text">Không có câu hỏi nào</div>
              <div className="table-empty-sub">Hàng đợi đang trống 🎉</div>
            </div>
          ) : (
            <div>
              {visible.map(i => (
                <button key={i.id} onClick={() => { setSelectedId(i.id); setRejecting(false); }} id={`review-item-${i.id}`}
                  className="review-list-item"
                  style={{
                    display: 'block', width: '100%', textAlign: 'left', padding: '14px 16px', border: 'none', cursor: 'pointer',
                    borderBottom: '1px solid var(--gray-100)',
                    background: selected?.id === i.id ? 'var(--brand-50)' : 'white',
                    borderLeft: `3px solid ${selected?.id === i.id ? 'var(--brand-500)' : 'transparent'}`,
                    transition: 'background .15s',
                  }}>
                  <div style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--gray-900)', marginBottom: 6, display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}>
                    {i.content}
                  </div>
                  <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', alignItems: 'center' }}>
                    <span className="badge badge-blue">{i.subject}</span>
                    <span className={clsx('badge', DIFF_LABEL[i.difficulty][1])}>{DIFF_LABEL[i.difficulty][0]}</span>
                    <span style={{ fontSize: 11.5, color: 'var(--gray-400)', marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 4 }}>
                      <Clock size={11} />{formatDateTime(i.submittedAt)}
                    </span>
                  </div>
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Detail */}
        <div className="card" style={{ padding: 24 }}>
          {!selected ? (
            <div className="table-empty"><div className="table-empty-text">Chọn một câu hỏi để xem chi tiết</div></div>
          ) : (
            <>
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 14 }}>
                <span className="badge badge-gray">{selected.id} · rev {selected.revision}</span>
                <span className={clsx('badge', SOURCE_LABEL[selected.source][1])}>{SOURCE_LABEL[selected.source][0]}</span>
                <span className="badge badge-blue">{selected.subject} / {selected.topic}</span>
                <span className={clsx('badge', DIFF_LABEL[selected.difficulty][1])}>{DIFF_LABEL[selected.difficulty][0]}</span>
                <span className="badge badge-teal">{selected.points} điểm</span>
              </div>

              <h2 style={{ fontSize: 17, fontWeight: 700, color: 'var(--gray-900)', lineHeight: 1.5, marginBottom: 18 }}>{selected.content}</h2>

              <div style={{ display: 'grid', gap: 8, marginBottom: 20 }}>
                {selected.options.map((o, idx) => {
                  const correct = idx === selected.correctIndex;
                  return (
                    <div key={idx} style={{
                      display: 'flex', alignItems: 'center', gap: 12, padding: '10px 14px', borderRadius: 10,
                      border: `1px solid ${correct ? 'var(--accent-500, #10b981)' : 'var(--gray-200)'}`,
                      background: correct ? 'var(--accent-50)' : 'white',
                    }}>
                      <span style={{
                        width: 26, height: 26, borderRadius: 8, display: 'flex', alignItems: 'center', justifyContent: 'center',
                        fontWeight: 700, fontSize: 12.5,
                        background: correct ? 'var(--accent-600)' : 'var(--gray-100)', color: correct ? 'white' : 'var(--gray-600)',
                      }}>{String.fromCharCode(65 + idx)}</span>
                      <span style={{ fontSize: 14, color: 'var(--gray-800)' }}>{o}</span>
                      {correct && <Check size={16} style={{ marginLeft: 'auto', color: 'var(--accent-600)' }} />}
                    </div>
                  );
                })}
              </div>

              <div style={{ fontSize: 12.5, color: 'var(--gray-500)', marginBottom: 20 }}>
                Tác giả <strong style={{ color: 'var(--gray-700)' }}>{selected.author}</strong> · gửi lúc {formatDateTime(selected.submittedAt)}
              </div>

              {selected.status === 'REJECTED' && selected.rejectReason && (
                <div style={{ background: 'var(--danger-50)', border: '1px solid var(--danger-200)', borderRadius: 8, padding: '10px 14px', fontSize: 13, color: 'var(--danger-700)' }}>
                  Lý do trả lại: {selected.rejectReason}
                </div>
              )}

              {selected.status === 'REVIEW_REQUIRED' && (
                <div style={{ borderTop: '1px solid var(--gray-100)', paddingTop: 18 }}>
                  {rejecting && (
                    <div className="form-group">
                      <label className="form-label" htmlFor="reject-reason">Lý do trả lại <span className="required">*</span></label>
                      <textarea id="reject-reason" className="form-input" rows={3} value={reason} onChange={e => setReason(e.target.value)}
                        placeholder="VD: Đáp án B cũng đúng, cần sửa phương án nhiễu" autoFocus style={{ resize: 'vertical' }} />
                    </div>
                  )}
                  <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end' }}>
                    {rejecting ? (
                      <>
                        <button className="btn btn-secondary" onClick={() => { setRejecting(false); setReason(''); }} id="btn-cancel-reject">Hủy</button>
                        <button className={clsx('btn btn-danger', busy && 'btn-loading')} disabled={busy} onClick={() => decide(false)} id="btn-confirm-reject">
                          <X size={14} />Xác nhận trả lại
                        </button>
                      </>
                    ) : (
                      <>
                        <button className="btn btn-secondary" onClick={() => setRejecting(true)} id="btn-reject"><X size={14} />Trả lại</button>
                        <button className={clsx('btn btn-success', busy && 'btn-loading')} disabled={busy} onClick={() => decide(true)} id="btn-approve">
                          <Check size={14} />Duyệt
                        </button>
                      </>
                    )}
                  </div>
                </div>
              )}
            </>
          )}
        </div>
      </div>

      <style>{`
        .review-list-item:hover { background: var(--gray-50) !important; }
        @media (max-width: 900px) { .review-grid { grid-template-columns: 1fr !important; } }
      `}</style>
    </div>
  );
}
