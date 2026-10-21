'use client';

import React, { useEffect, useMemo, useState, useCallback } from 'react';
import { BookOpen, Plus, Search, RefreshCw, Database, Pencil, History, Trash2, Check } from 'lucide-react';
import clsx from 'clsx';
import { useToast } from '@/contexts/ToastContext';
import { fetchOrMock, formatDateTime } from '@/lib/fetchOrMock';

/* ============================================================
   TYPES & MOCK
   ============================================================ */

export type Difficulty = 'EASY' | 'MEDIUM' | 'HARD';
type QStatus = 'DRAFT' | 'REVIEW_REQUIRED' | 'APPROVED' | 'REJECTED';

interface Question {
  id: string;
  revision: number;
  content: string;
  options: string[];
  correctIndex: number;
  subject: string;
  topic: string;
  difficulty: Difficulty;
  points: number;
  status: QStatus;
  author: string;
  updatedAt: string;
}

const MOCK: Question[] = [
  { id: 'q-1001', revision: 2, content: 'Đạo hàm của hàm số y = x³ − 3x + 2 là?', options: ['3x² − 3', '3x² + 3', 'x² − 3', '3x − 3'], correctIndex: 0, subject: 'Toán', topic: 'Đạo hàm', difficulty: 'EASY', points: 0.25, status: 'REVIEW_REQUIRED', author: 'gv.nguyen.van.a', updatedAt: '2026-10-16T08:12:00Z' },
  { id: 'q-1002', revision: 1, content: 'Nguyên tố nào có độ âm điện lớn nhất?', options: ['O', 'Cl', 'F', 'N'], correctIndex: 2, subject: 'Hóa học', topic: 'Bảng tuần hoàn', difficulty: 'MEDIUM', points: 0.25, status: 'APPROVED', author: 'gv.tran.thi.b', updatedAt: '2026-10-16T09:40:00Z' },
  { id: 'q-1003', revision: 3, content: 'Vật dao động điều hòa x = 5cos(4πt) cm. Chu kỳ là?', options: ['0,25 s', '0,5 s', '2 s', '4 s'], correctIndex: 1, subject: 'Vật lý', topic: 'Dao động cơ', difficulty: 'MEDIUM', points: 0.25, status: 'APPROVED', author: 'gv.le.van.c', updatedAt: '2026-10-15T14:05:00Z' },
  { id: 'q-1004', revision: 1, content: 'Số nghiệm thực của phương trình log₂(x² − 1) = 3 là?', options: ['0', '1', '2', '3'], correctIndex: 2, subject: 'Toán', topic: 'Logarit', difficulty: 'HARD', points: 0.5, status: 'APPROVED', author: 'gv.nguyen.van.a', updatedAt: '2026-10-15T10:22:00Z' },
  { id: 'q-1005', revision: 1, content: 'Tác phẩm "Vợ chồng A Phủ" của tác giả nào?', options: ['Kim Lân', 'Tô Hoài', 'Nam Cao', 'Nguyễn Tuân'], correctIndex: 1, subject: 'Ngữ văn', topic: 'Văn học hiện đại', difficulty: 'EASY', points: 0.25, status: 'REJECTED', author: 'gv.pham.d', updatedAt: '2026-10-14T16:48:00Z' },
  { id: 'q-1006', revision: 1, content: 'Cho cấp số cộng u₁ = 2, d = 3. Số hạng u₁₀ bằng?', options: ['29', '30', '32', '27'], correctIndex: 0, subject: 'Toán', topic: 'Dãy số', difficulty: 'EASY', points: 0.25, status: 'DRAFT', author: 'gv.nguyen.van.a', updatedAt: '2026-10-17T01:10:00Z' },
];

export const DIFF: Record<Difficulty, [string, string]> = {
  EASY: ['Dễ', 'badge-green'], MEDIUM: ['Trung bình', 'badge-amber'], HARD: ['Khó', 'badge-red'],
};
const STATUS: Record<QStatus, [string, string]> = {
  DRAFT: ['Nháp', 'badge-gray'], REVIEW_REQUIRED: ['Chờ duyệt', 'badge-blue'], APPROVED: ['Đã duyệt', 'badge-green'], REJECTED: ['Bị trả lại', 'badge-red'],
};

/* ============================================================
   PAGE
   ============================================================ */

export default function QuestionsPage() {
  const { success } = useToast();
  const [items, setItems] = useState<Question[]>([]);
  const [isMock, setIsMock] = useState(false);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [subject, setSubject] = useState('');
  const [difficulty, setDifficulty] = useState<'' | Difficulty>('');
  const [status, setStatus] = useState<'' | QStatus>('');
  const [editing, setEditing] = useState<Question | null>(null);
  const [history, setHistory] = useState<Question | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    const r = await fetchOrMock<Question[]>('/api/v1/questions', MOCK);
    setItems(r.data); setIsMock(r.isMock); setLoading(false);
  }, []);
  useEffect(() => { load(); }, [load]);

  const subjects = useMemo(() => Array.from(new Set(items.map(i => i.subject))), [items]);
  const filtered = items.filter(i =>
    (!search || `${i.content} ${i.topic} ${i.id}`.toLowerCase().includes(search.toLowerCase())) &&
    (!subject || i.subject === subject) && (!difficulty || i.difficulty === difficulty) && (!status || i.status === status));

  const stats = (['APPROVED', 'REVIEW_REQUIRED', 'DRAFT', 'REJECTED'] as QStatus[]).map(s => ({ s, n: items.filter(i => i.status === s).length }));

  const save = (q: Question, submit: boolean) => {
    const exists = items.some(i => i.id === q.id);
    const next: Question = { ...q, revision: exists ? q.revision + 1 : 1, status: submit ? 'REVIEW_REQUIRED' : 'DRAFT', updatedAt: new Date().toISOString() };
    setItems(prev => exists ? prev.map(i => i.id === q.id ? next : i) : [next, ...prev]);
    setEditing(null);
    success(submit ? 'Đã gửi duyệt câu hỏi' : 'Đã lưu nháp');
  };

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Ngân hàng câu hỏi</h1>
        <p className="page-subtitle">Quản lý câu hỏi trắc nghiệm, metadata và lịch sử revision</p>
        <div className="page-actions">
          {isMock && <span className="badge badge-amber"><Database size={12} />Dữ liệu mẫu</span>}
          <button className="btn btn-secondary" onClick={load} id="btn-refresh-questions"><RefreshCw size={14} /></button>
          <button className="btn btn-primary" onClick={() => setEditing(blank())} id="btn-create-question"><Plus size={14} />Thêm câu hỏi</button>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 12, marginBottom: 20 }}>
        {stats.map(({ s, n }) => (
          <button key={s} className={clsx('stat-card', status === s && 'q-stat-active')} onClick={() => setStatus(status === s ? '' : s)} id={`stat-${s}`}
            style={{ cursor: 'pointer', textAlign: 'left', border: '1px solid var(--gray-200)' }}>
            <div className="stat-content">
              <div className="stat-value">{n}</div>
              <div className="stat-label"><span className={clsx('badge', STATUS[s][1])}>{STATUS[s][0]}</span></div>
            </div>
          </button>
        ))}
      </div>

      <div className="table-container">
        <div className="table-toolbar" style={{ flexWrap: 'wrap', gap: 10 }}>
          <div className="search-bar" style={{ width: 280 }}>
            <Search size={14} className="search-bar-icon" />
            <input placeholder="Tìm nội dung, chủ đề, mã…" value={search} onChange={e => setSearch(e.target.value)} id="question-search" />
          </div>
          <select className="form-select" style={{ width: 150 }} value={subject} onChange={e => setSubject(e.target.value)} id="filter-subject" aria-label="Môn học">
            <option value="">Tất cả môn</option>{subjects.map(s => <option key={s}>{s}</option>)}
          </select>
          <select className="form-select" style={{ width: 140 }} value={difficulty} onChange={e => setDifficulty(e.target.value as Difficulty | '')} id="filter-difficulty" aria-label="Độ khó">
            <option value="">Mọi độ khó</option>{(Object.keys(DIFF) as Difficulty[]).map(d => <option key={d} value={d}>{DIFF[d][0]}</option>)}
          </select>
          <span style={{ marginLeft: 'auto', fontSize: 12.5, color: 'var(--gray-400)' }}>{filtered.length} câu hỏi</span>
        </div>

        {loading ? (
          <div style={{ padding: 16 }}>{[0, 1, 2, 3].map(i => <div key={i} className="skeleton" style={{ height: 44, marginBottom: 8, borderRadius: 6 }} />)}</div>
        ) : filtered.length === 0 ? (
          <div className="table-empty">
            <div className="table-empty-icon"><BookOpen /></div>
            <div className="table-empty-text">Không có câu hỏi phù hợp</div>
            <div className="table-empty-sub">Thử bộ lọc khác hoặc thêm câu hỏi mới</div>
          </div>
        ) : (
          <table>
            <thead><tr><th>Câu hỏi</th><th>Môn / Chủ đề</th><th>Độ khó</th><th>Điểm</th><th>Trạng thái</th><th>Cập nhật</th><th style={{ width: 110 }}></th></tr></thead>
            <tbody>
              {filtered.map(q => (
                <tr key={q.id}>
                  <td style={{ maxWidth: 380 }}>
                    <div style={{ fontWeight: 600, color: 'var(--gray-900)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{q.content}</div>
                    <div style={{ fontSize: 11.5, color: 'var(--gray-400)' }}>{q.id} · rev {q.revision} · {q.author}</div>
                  </td>
                  <td><div style={{ fontWeight: 500 }}>{q.subject}</div><div style={{ fontSize: 12, color: 'var(--gray-500)' }}>{q.topic}</div></td>
                  <td><span className={clsx('badge', DIFF[q.difficulty][1])}>{DIFF[q.difficulty][0]}</span></td>
                  <td style={{ fontWeight: 600 }}>{q.points}</td>
                  <td><span className={clsx('badge', STATUS[q.status][1])}><span className="badge-dot" />{STATUS[q.status][0]}</span></td>
                  <td style={{ fontSize: 12.5, color: 'var(--gray-500)', whiteSpace: 'nowrap' }}>{formatDateTime(q.updatedAt)}</td>
                  <td>
                    <div style={{ display: 'flex', gap: 4 }}>
                      <button className="topbar-icon-btn" onClick={() => setEditing(q)} aria-label="Sửa" id={`btn-edit-${q.id}`}><Pencil size={14} /></button>
                      <button className="topbar-icon-btn" onClick={() => setHistory(q)} aria-label="Lịch sử" id={`btn-history-${q.id}`}><History size={14} /></button>
                      {q.status === 'DRAFT' && (
                        <button className="topbar-icon-btn" onClick={() => { setItems(p => p.filter(i => i.id !== q.id)); success('Đã xóa nháp'); }} aria-label="Xóa" id={`btn-del-${q.id}`}><Trash2 size={14} /></button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {editing && <QuestionModal initial={editing} onClose={() => setEditing(null)} onSave={save} />}
      {history && <HistoryModal q={history} onClose={() => setHistory(null)} />}
      <style>{`.q-stat-active { border-color: var(--brand-500) !important; box-shadow: 0 0 0 3px var(--brand-50); }`}</style>
    </div>
  );
}

const blank = (): Question => ({
  id: `q-${Math.floor(Math.random() * 9000 + 2000)}`, revision: 0, content: '', options: ['', '', '', ''], correctIndex: 0,
  subject: 'Toán', topic: '', difficulty: 'MEDIUM', points: 0.25, status: 'DRAFT', author: 'bạn', updatedAt: new Date().toISOString(),
});

/* ============================================================
   MODALS
   ============================================================ */

function QuestionModal({ initial, onClose, onSave }: { initial: Question; onClose: () => void; onSave: (q: Question, submit: boolean) => void }) {
  const [q, setQ] = useState(initial);
  const [err, setErr] = useState('');
  const isNew = initial.revision === 0;

  const submit = (send: boolean) => {
    if (!q.content.trim()) return setErr('Chưa nhập nội dung câu hỏi');
    if (q.options.some(o => !o.trim())) return setErr('Cần nhập đủ 4 phương án');
    if (new Set(q.options.map(o => o.trim())).size !== q.options.length) return setErr('Các phương án không được trùng nhau');
    if (!q.topic.trim()) return setErr('Chưa nhập chủ đề');
    onSave(q, send);
  };

  return (
    <div className="modal-overlay" onClick={e => e.target === e.currentTarget && onClose()}>
      <div className="modal modal-lg">
        <div className="modal-header">
          <div>
            <div className="modal-title">{isNew ? 'Thêm câu hỏi' : `Sửa ${q.id}`}</div>
            <div className="modal-subtitle">{isNew ? 'Câu hỏi mới sẽ ở trạng thái nháp' : `Lưu sẽ tạo revision ${q.revision + 1} — revision cũ được giữ nguyên`}</div>
          </div>
          <button className="modal-close" onClick={onClose} id="btn-close-question">✕</button>
        </div>
        <div className="modal-body">
          {err && <div style={{ background: 'var(--danger-50)', border: '1px solid var(--danger-200)', borderRadius: 8, padding: '10px 14px', fontSize: 13, color: 'var(--danger-700)', marginBottom: 12 }}>⚠ {err}</div>}
          <div className="form-group">
            <label className="form-label" htmlFor="q-content">Nội dung <span className="required">*</span></label>
            <textarea id="q-content" className="form-input" rows={3} value={q.content} onChange={e => { setQ({ ...q, content: e.target.value }); setErr(''); }} style={{ resize: 'vertical' }} autoFocus />
          </div>
          <label className="form-label">Phương án (chọn đáp án đúng) <span className="required">*</span></label>
          <div style={{ display: 'grid', gap: 8, marginBottom: 16 }}>
            {q.options.map((o, i) => (
              <div key={i} style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <button type="button" onClick={() => setQ({ ...q, correctIndex: i })} id={`q-correct-${i}`} aria-label={`Đáp án ${String.fromCharCode(65 + i)} đúng`}
                  style={{ width: 32, height: 32, borderRadius: 8, border: 'none', cursor: 'pointer', fontWeight: 700, flexShrink: 0,
                    background: q.correctIndex === i ? 'var(--accent-600)' : 'var(--gray-100)', color: q.correctIndex === i ? 'white' : 'var(--gray-600)' }}>
                  {q.correctIndex === i ? <Check size={15} /> : String.fromCharCode(65 + i)}
                </button>
                <input className="form-input" value={o} id={`q-option-${i}`} placeholder={`Phương án ${String.fromCharCode(65 + i)}`}
                  onChange={e => { const opts = [...q.options]; opts[i] = e.target.value; setQ({ ...q, options: opts }); setErr(''); }} />
              </div>
            ))}
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 12 }}>
            <div className="form-group"><label className="form-label" htmlFor="q-subject">Môn</label>
              <input id="q-subject" className="form-input" value={q.subject} onChange={e => setQ({ ...q, subject: e.target.value })} /></div>
            <div className="form-group"><label className="form-label" htmlFor="q-topic">Chủ đề</label>
              <input id="q-topic" className="form-input" value={q.topic} onChange={e => setQ({ ...q, topic: e.target.value })} /></div>
            <div className="form-group"><label className="form-label" htmlFor="q-diff">Độ khó</label>
              <select id="q-diff" className="form-select" value={q.difficulty} onChange={e => setQ({ ...q, difficulty: e.target.value as Difficulty })}>
                {(Object.keys(DIFF) as Difficulty[]).map(d => <option key={d} value={d}>{DIFF[d][0]}</option>)}
              </select></div>
            <div className="form-group"><label className="form-label" htmlFor="q-points">Điểm</label>
              <input id="q-points" type="number" step={0.05} min={0.05} className="form-input" value={q.points} onChange={e => setQ({ ...q, points: Number(e.target.value) })} /></div>
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn btn-secondary" onClick={onClose} id="btn-cancel-question">Hủy</button>
          <button className="btn btn-secondary" onClick={() => submit(false)} id="btn-save-draft">Lưu nháp</button>
          <button className="btn btn-primary" onClick={() => submit(true)} id="btn-submit-review">Gửi duyệt</button>
        </div>
      </div>
    </div>
  );
}

function HistoryModal({ q, onClose }: { q: Question; onClose: () => void }) {
  const revs = Array.from({ length: q.revision }, (_, i) => q.revision - i);
  return (
    <div className="modal-overlay" onClick={e => e.target === e.currentTarget && onClose()}>
      <div className="modal">
        <div className="modal-header">
          <div><div className="modal-title">Lịch sử revision</div><div className="modal-subtitle">{q.id} · revision là bất biến</div></div>
          <button className="modal-close" onClick={onClose} id="btn-close-history">✕</button>
        </div>
        <div className="modal-body">
          {revs.map((r, idx) => (
            <div key={r} style={{ display: 'flex', gap: 14, paddingBottom: 16, position: 'relative' }}>
              <div style={{ width: 28, height: 28, borderRadius: 99, flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 700,
                background: idx === 0 ? 'var(--brand-600)' : 'var(--gray-100)', color: idx === 0 ? 'white' : 'var(--gray-500)' }}>{r}</div>
              <div>
                <div style={{ fontWeight: 600, fontSize: 13.5 }}>Revision {r} {idx === 0 && <span className="badge badge-blue" style={{ marginLeft: 6 }}>Hiện tại</span>}</div>
                <div style={{ fontSize: 12.5, color: 'var(--gray-500)' }}>{q.author} · {idx === 0 ? formatDateTime(q.updatedAt) : 'trước đó'}</div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
