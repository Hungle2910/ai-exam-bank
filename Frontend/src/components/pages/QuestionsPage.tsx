'use client';

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { BookOpen, Plus, Search, RefreshCw, Database, Pencil, History, Check } from 'lucide-react';
import clsx from 'clsx';
import { useToast } from '@/contexts/ToastContext';
import { useAuth } from '@/contexts/AuthContext';
import { ApiError, fetchOrMock, formatDateTime } from '@/lib/fetchOrMock';

/* ============================================================
   TYPES
   ============================================================ */

type Difficulty = 'EASY' | 'MEDIUM' | 'HARD';
type QStatus = 'DRAFT' | 'REVIEW_REQUIRED' | 'APPROVED' | 'REJECTED';
type Origin = 'MANUAL' | 'IMPORT' | 'AI_DRAFT' | 'SEED';

interface Ref { id: string; name: string }
interface CatalogTopic { id: string; code: string; name: string }
interface CatalogSubject { id: string; code: string; name: string; canWrite: boolean; topics: CatalogTopic[] }

interface QuestionItem {
  id: string; revision: number; status: QStatus; stem: string;
  subject: Ref; topic: Ref; difficulty: Difficulty; ownerUserId: string; updatedAt: string;
}
interface RevisionInfo { revision: number; status: QStatus; origin: Origin; createdByUserId: string; createdAt: string }
interface QuestionDetail extends QuestionItem {
  origin: Origin; options: string[]; correctOption: string; source: string | null; revisions: RevisionInfo[];
}
interface Paged { items: QuestionItem[]; page: number; pageSize: number; total: number }
interface Filters { subjectId: string; topicId: string; difficulty: '' | Difficulty; status: '' | QStatus; text: string }
interface ServerError { code: string; field: string; message: string }

const PAGE_SIZE = 20;
const LABELS = 'ABCD';
const NO_FILTERS: Filters = { subjectId: '', topicId: '', difficulty: '', status: '', text: '' };

const DIFF: Record<Difficulty, [string, string]> = {
  EASY: ['Dễ', 'badge-green'], MEDIUM: ['Trung bình', 'badge-amber'], HARD: ['Khó', 'badge-red'],
};
const STATUS: Record<QStatus, [string, string]> = {
  DRAFT: ['Nháp', 'badge-gray'], REVIEW_REQUIRED: ['Chờ duyệt', 'badge-blue'], APPROVED: ['Đã duyệt', 'badge-green'], REJECTED: ['Bị trả lại', 'badge-red'],
};
const ORIGIN: Record<Origin, string> = { MANUAL: 'Nhập tay', IMPORT: 'Import', AI_DRAFT: 'AI nháp', SEED: 'Dữ liệu mẫu' };

/* ============================================================
   SAMPLE DATA (only used when the API is not available in development)
   ============================================================ */

const MOCK_CATALOG: CatalogSubject[] = [
  { id: 'sub-math', code: 'MATH', name: 'Toán', canWrite: true, topics: [
    { id: 'top-deriv', code: 'MATH.DERIV_APP', name: 'Ứng dụng đạo hàm để khảo sát hàm số' },
    { id: 'top-integral', code: 'MATH.INTEGRAL', name: 'Nguyên hàm và tích phân' },
    { id: 'top-oxyz', code: 'MATH.OXYZ', name: 'Phương pháp tọa độ trong không gian' }] },
  { id: 'sub-phys', code: 'PHYS', name: 'Vật lý', canWrite: true, topics: [
    { id: 'top-gas', code: 'PHYS.IDEAL_GAS', name: 'Khí lí tưởng' },
    { id: 'top-mag', code: 'PHYS.MAGNETIC', name: 'Từ trường' },
    { id: 'top-nuclear', code: 'PHYS.NUCLEAR', name: 'Vật lí hạt nhân và phóng xạ' }] },
  { id: 'sub-chem', code: 'CHEM', name: 'Hóa học', canWrite: true, topics: [
    { id: 'top-ester', code: 'CHEM.ESTER_LIPID', name: 'Ester – Lipid' },
    { id: 'top-carb', code: 'CHEM.CARBOHYDRATE', name: 'Carbohydrate' },
    { id: 'top-electro', code: 'CHEM.ELECTROCHEM', name: 'Pin điện và điện phân' }] },
];

const mockQuestion = (
  id: string, subject: Ref, topic: Ref, difficulty: Difficulty, status: QStatus, stem: string,
  options: string[], correctOption: string, updatedAt: string,
): QuestionDetail => ({
  id, revision: 1, status, stem, subject, topic, difficulty, ownerUserId: 'mock-user', updatedAt, origin: 'MANUAL',
  options, correctOption, source: null,
  revisions: [{ revision: 1, status, origin: 'MANUAL', createdByUserId: 'mock-user', createdAt: updatedAt }],
});

const MOCK_QUESTIONS: QuestionDetail[] = [
  mockQuestion('q-1001', { id: 'sub-math', name: 'Toán' }, { id: 'top-deriv', name: 'Ứng dụng đạo hàm để khảo sát hàm số' }, 'EASY', 'DRAFT',
    'Hàm số y = x³ − 3x + 2 đồng biến trên khoảng nào?', ['(−∞; −1) và (1; +∞)', '(−1; 1)', '(−∞; 1)', '(−1; +∞)'], 'A', '2026-10-17T01:10:00Z'),
  mockQuestion('q-1002', { id: 'sub-math', name: 'Toán' }, { id: 'top-integral', name: 'Nguyên hàm và tích phân' }, 'EASY', 'APPROVED',
    'Giá trị của tích phân ∫₀¹ 2x dx bằng bao nhiêu?', ['0', '1', '2', '1/2'], 'B', '2026-10-16T09:40:00Z'),
  mockQuestion('q-1003', { id: 'sub-phys', name: 'Vật lý' }, { id: 'top-gas', name: 'Khí lí tưởng' }, 'EASY', 'REVIEW_REQUIRED',
    'Trong quá trình đẳng nhiệt của một lượng khí lí tưởng xác định, đại lượng nào không đổi?', ['Nhiệt độ', 'Áp suất', 'Thể tích', 'Khối lượng riêng'], 'A', '2026-10-16T08:12:00Z'),
  mockQuestion('q-1004', { id: 'sub-chem', name: 'Hóa học' }, { id: 'top-ester', name: 'Ester – Lipid' }, 'EASY', 'APPROVED',
    'Chất nào sau đây là ester?', ['CH₃COOC₂H₅', 'C₂H₅OH', 'CH₃COOH', 'CH₃CHO'], 'A', '2026-10-15T14:05:00Z'),
];

const toItem = (q: QuestionDetail): QuestionItem => ({
  id: q.id, revision: q.revision, status: q.status, stem: q.stem, subject: q.subject, topic: q.topic,
  difficulty: q.difficulty, ownerUserId: q.ownerUserId, updatedAt: q.updatedAt,
});

function mockPage(store: QuestionDetail[], f: Filters, page: number): Paged {
  const text = f.text.trim().toLowerCase();
  const rows = store
    .filter(q => (!f.subjectId || q.subject.id === f.subjectId) && (!f.topicId || q.topic.id === f.topicId)
      && (!f.difficulty || q.difficulty === f.difficulty) && (!f.status || q.status === f.status)
      && (!text || q.stem.toLowerCase().includes(text) || q.id.toLowerCase() === text))
    .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
  return { items: rows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE).map(toItem), page, pageSize: PAGE_SIZE, total: rows.length };
}

const describe = (e: unknown, what: string) =>
  e instanceof ApiError && e.status === 401 ? 'Phiên đăng nhập đã hết hạn, hãy đăng nhập lại'
    : e instanceof ApiError && e.status === 403 ? `Bạn không có quyền ${what}`
      : e instanceof ApiError && e.status === 0 ? 'Không kết nối được máy chủ'
        : `Không thể ${what}`;

/* ============================================================
   PAGE
   ============================================================ */

export default function QuestionsPage() {
  const { success } = useToast();
  const { user } = useAuth();
  const latestLoad = useRef(0);
  const [catalog, setCatalog] = useState<CatalogSubject[]>([]);
  const [mockStore, setMockStore] = useState<QuestionDetail[]>(MOCK_QUESTIONS);
  const [data, setData] = useState<Paged | null>(null);
  const [isMock, setIsMock] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filters, setFilters] = useState<Filters>(NO_FILTERS);
  const [searchInput, setSearchInput] = useState('');
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<string | 'new' | null>(null);
  const [history, setHistory] = useState<string | null>(null);

  useEffect(() => {
    fetchOrMock<CatalogSubject[]>('/api/v1/catalog', MOCK_CATALOG).then(r => setCatalog(r.data)).catch(() => setCatalog([]));
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => { setFilters(f => f.text === searchInput ? f : { ...f, text: searchInput }); setPage(1); }, 300);
    return () => clearTimeout(timer);
  }, [searchInput]);

  const load = useCallback(async () => {
    // Only the newest request may update the page: an older, slower response must not overwrite it.
    const requestId = ++latestLoad.current;
    setLoading(true); setError(null);
    const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) });
    if (filters.subjectId) params.set('subjectId', filters.subjectId);
    if (filters.topicId) params.set('topicId', filters.topicId);
    if (filters.difficulty) params.set('difficulty', filters.difficulty);
    if (filters.status) params.set('status', filters.status);
    if (filters.text.trim()) params.set('q', filters.text.trim());
    try {
      const r = await fetchOrMock<Paged>(`/api/v1/questions?${params}`, mockPage(mockStore, filters, page));
      if (requestId !== latestLoad.current) return;
      setData(r.data); setIsMock(r.isMock);
    } catch (e) {
      if (requestId !== latestLoad.current) return;
      setData(null); setError(describe(e, 'tải danh sách câu hỏi'));
    } finally { if (requestId === latestLoad.current) setLoading(false); }
  }, [filters, page, mockStore]);
  useEffect(() => { load(); }, [load]);

  const topics = catalog.find(s => s.id === filters.subjectId)?.topics ?? [];
  const setFilter = (patch: Partial<Filters>) => { setFilters(f => ({ ...f, ...patch })); setPage(1); };
  const clearFilters = () => { setFilters(NO_FILTERS); setSearchInput(''); setPage(1); };
  const hasFilters = Object.values(filters).some(Boolean);
  // A department head sees subjects to filter by them but cannot write; editing is for the owner outside review.
  const canCreate = isMock || catalog.some(s => s.canWrite);
  const canEdit = (q: QuestionItem) =>
    q.status !== 'REVIEW_REQUIRED' && (isMock || (!!user && q.ownerUserId.toLowerCase() === user.id.toLowerCase()));
  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  const onSaved = (mockDetail?: QuestionDetail) => {
    if (mockDetail) setMockStore(prev => prev.some(q => q.id === mockDetail.id) ? prev.map(q => q.id === mockDetail.id ? mockDetail : q) : [mockDetail, ...prev]);
    setEditing(null); load();
  };

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Ngân hàng câu hỏi</h1>
        <p className="page-subtitle">Câu hỏi trắc nghiệm theo môn, chủ đề và độ khó; mỗi lần lưu tạo một revision mới ở trạng thái nháp</p>
        <div className="page-actions">
          {isMock && <span className="badge badge-amber"><Database size={12} />Dữ liệu mẫu</span>}
          <button className="btn btn-secondary" onClick={load} id="btn-refresh-questions" aria-label="Làm mới"><RefreshCw size={14} /></button>
          {canCreate && <button className="btn btn-primary" onClick={() => setEditing('new')} id="btn-create-question"><Plus size={14} />Thêm câu hỏi</button>}
        </div>
      </div>

      <div className="table-container">
        <div className="table-toolbar" style={{ flexWrap: 'wrap', gap: 10 }}>
          <div className="search-bar" style={{ width: 280 }}>
            <Search size={14} className="search-bar-icon" />
            <input placeholder="Tìm nội dung hoặc mã câu hỏi…" value={searchInput} onChange={e => setSearchInput(e.target.value)} id="question-search" />
          </div>
          <select className="form-select" style={{ width: 150 }} value={filters.subjectId} onChange={e => setFilter({ subjectId: e.target.value, topicId: '' })} id="filter-subject" aria-label="Môn học">
            <option value="">Tất cả môn</option>{catalog.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
          <select className="form-select" style={{ width: 200 }} value={filters.topicId} disabled={!filters.subjectId} onChange={e => setFilter({ topicId: e.target.value })} id="filter-topic" aria-label="Chủ đề">
            <option value="">Tất cả chủ đề</option>{topics.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
          <select className="form-select" style={{ width: 140 }} value={filters.difficulty} onChange={e => setFilter({ difficulty: e.target.value as '' | Difficulty })} id="filter-difficulty" aria-label="Độ khó">
            <option value="">Mọi độ khó</option>{(Object.keys(DIFF) as Difficulty[]).map(d => <option key={d} value={d}>{DIFF[d][0]}</option>)}
          </select>
          <select className="form-select" style={{ width: 140 }} value={filters.status} onChange={e => setFilter({ status: e.target.value as '' | QStatus })} id="filter-status" aria-label="Trạng thái">
            <option value="">Mọi trạng thái</option>{(Object.keys(STATUS) as QStatus[]).map(s => <option key={s} value={s}>{STATUS[s][0]}</option>)}
          </select>
          <span style={{ marginLeft: 'auto', fontSize: 12.5, color: 'var(--gray-400)' }}>{data ? `${data.total} câu hỏi` : ''}</span>
        </div>

        {loading ? (
          <div style={{ padding: 16 }}>{[0, 1, 2, 3].map(i => <div key={i} className="skeleton" style={{ height: 44, marginBottom: 8, borderRadius: 6 }} />)}</div>
        ) : error ? (
          <div className="table-empty" role="alert">
            <div className="table-empty-icon"><BookOpen /></div>
            <div className="table-empty-text">{error}</div>
            <div className="table-empty-sub"><button className="btn btn-secondary btn-sm" onClick={load} id="btn-retry-questions">Thử lại</button></div>
          </div>
        ) : !data || data.items.length === 0 ? (
          <div className="table-empty">
            <div className="table-empty-icon"><BookOpen /></div>
            <div className="table-empty-text">{hasFilters ? 'Không có câu hỏi phù hợp' : 'Chưa có câu hỏi nào'}</div>
            <div className="table-empty-sub">
              {hasFilters
                ? <button className="btn btn-secondary btn-sm" onClick={clearFilters} id="btn-clear-filters">Xóa bộ lọc</button>
                : canCreate && <button className="btn btn-primary btn-sm" onClick={() => setEditing('new')} id="btn-empty-create">Thêm câu hỏi</button>}
            </div>
          </div>
        ) : (
          <table>
            <thead><tr><th>Câu hỏi</th><th>Môn / Chủ đề</th><th>Độ khó</th><th>Trạng thái</th><th>Cập nhật</th><th style={{ width: 90 }}></th></tr></thead>
            <tbody>
              {data.items.map(q => (
                <tr key={q.id}>
                  <td style={{ maxWidth: 380 }}>
                    <div style={{ fontWeight: 600, color: 'var(--gray-900)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{q.stem}</div>
                    <div style={{ fontSize: 11.5, color: 'var(--gray-400)' }}>{q.id} · rev {q.revision}</div>
                  </td>
                  <td><div style={{ fontWeight: 500 }}>{q.subject.name}</div><div style={{ fontSize: 12, color: 'var(--gray-500)' }}>{q.topic.name}</div></td>
                  <td><span className={clsx('badge', DIFF[q.difficulty][1])}>{DIFF[q.difficulty][0]}</span></td>
                  <td><span className={clsx('badge', STATUS[q.status][1])}><span className="badge-dot" />{STATUS[q.status][0]}</span></td>
                  <td style={{ fontSize: 12.5, color: 'var(--gray-500)', whiteSpace: 'nowrap' }}>{formatDateTime(q.updatedAt)}</td>
                  <td>
                    <div style={{ display: 'flex', gap: 4 }}>
                      {canEdit(q) && <button className="topbar-icon-btn" onClick={() => setEditing(q.id)} aria-label="Sửa" id={`btn-edit-${q.id}`}><Pencil size={14} /></button>}
                      <button className="topbar-icon-btn" onClick={() => setHistory(q.id)} aria-label="Lịch sử" id={`btn-history-${q.id}`}><History size={14} /></button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {data && data.total > data.pageSize && (
          <div style={{ display: 'flex', gap: 10, alignItems: 'center', justifyContent: 'flex-end', padding: 12 }}>
            <button className="btn btn-secondary btn-sm" disabled={page <= 1} onClick={() => setPage(p => p - 1)} id="btn-prev-page">Trước</button>
            <span style={{ fontSize: 12.5, color: 'var(--gray-500)' }}>Trang {page} / {totalPages}</span>
            <button className="btn btn-secondary btn-sm" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)} id="btn-next-page">Sau</button>
          </div>
        )}
      </div>

      {editing && (
        <QuestionModal id={editing === 'new' ? null : editing} catalog={catalog} isMock={isMock} mockStore={mockStore}
          onClose={() => setEditing(null)} onSaved={d => { success(editing === 'new' ? 'Đã lưu câu hỏi ở trạng thái nháp' : 'Đã lưu revision mới ở trạng thái nháp'); onSaved(d); }} />
      )}
      {history && <HistoryModal id={history} isMock={isMock} mockStore={mockStore} onClose={() => setHistory(null)} />}
    </div>
  );
}

/* ============================================================
   DETAIL LOADING (shared by editor and history)
   ============================================================ */

function useDetail(id: string | null, isMock: boolean, mockStore: QuestionDetail[]) {
  const [detail, setDetail] = useState<QuestionDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(id !== null);

  const load = useCallback(async () => {
    if (id === null) return;
    setLoading(true); setError(null);
    try {
      if (isMock) {
        const found = mockStore.find(q => q.id === id);
        if (!found) throw new ApiError(404, 'not found');
        setDetail(found);
      } else {
        const res = await fetch(`/api/v1/questions/${id}`, { credentials: 'include' });
        if (!res.ok) throw new ApiError(res.status, `API lỗi ${res.status}`);
        setDetail(await res.json() as QuestionDetail);
      }
    } catch (e) {
      setError(e instanceof ApiError && e.status === 404 ? 'Không tìm thấy câu hỏi' : describe(e, 'tải câu hỏi'));
    } finally { setLoading(false); }
  }, [id, isMock, mockStore]);
  // The store is read once per open; later edits to the sample data must not reset a form in progress.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { load(); }, [id, isMock]);

  return { detail, error, loading, reload: load };
}

/* ============================================================
   MODALS
   ============================================================ */

function QuestionModal({ id, catalog, isMock, mockStore, onClose, onSaved }: {
  id: string | null; catalog: CatalogSubject[]; isMock: boolean; mockStore: QuestionDetail[];
  onClose: () => void; onSaved: (mockDetail?: QuestionDetail) => void;
}) {
  const { detail, error: loadError, loading, reload } = useDetail(id, isMock, mockStore);
  const isNew = id === null;
  return (
    <div className="modal-overlay" onClick={e => e.target === e.currentTarget && onClose()}>
      <div className="modal modal-lg">
        {loading ? <div className="modal-body"><div className="skeleton" style={{ height: 240, borderRadius: 8 }} /></div>
          : loadError ? (
            <div className="modal-body" role="alert">
              <div style={{ marginBottom: 12 }}>⚠ {loadError}</div>
              <button className="btn btn-secondary btn-sm" onClick={reload} id="btn-retry-detail">Thử lại</button>{' '}
              <button className="btn btn-secondary btn-sm" onClick={onClose} id="btn-close-question">Đóng</button>
            </div>
          ) : <QuestionForm key={detail?.id ?? 'new'} initial={isNew ? null : detail} catalog={catalog} isMock={isMock} onClose={onClose} onSaved={onSaved} onReload={reload} />}
      </div>
    </div>
  );
}

function QuestionForm({ initial, catalog, isMock, onClose, onSaved, onReload }: {
  initial: QuestionDetail | null; catalog: CatalogSubject[]; isMock: boolean;
  onClose: () => void; onSaved: (mockDetail?: QuestionDetail) => void; onReload: () => void;
}) {
  const { csrfToken } = useAuth();
  const isNew = initial === null;
  const [stem, setStem] = useState(initial?.stem ?? '');
  const [options, setOptions] = useState<string[]>(initial?.options ?? ['', '', '', '']);
  const [correct, setCorrect] = useState(initial ? Math.max(0, LABELS.indexOf(initial.correctOption)) : 0);
  const [subjectId, setSubjectId] = useState(initial?.subject.id ?? '');
  const [topicId, setTopicId] = useState(initial?.topic.id ?? '');
  const [difficulty, setDifficulty] = useState<Difficulty>(initial?.difficulty ?? 'MEDIUM');
  const [source, setSource] = useState(initial?.source ?? '');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [saving, setSaving] = useState(false);

  const topics = useMemo(() => catalog.find(s => s.id === subjectId)?.topics ?? [], [catalog, subjectId]);
  const clear = (field: string) => { setErrors(e => { const next = { ...e }; delete next[field]; return next; }); setBanner(null); };

  const validate = () => {
    const e: Record<string, string> = {};
    if (!stem.trim()) e.stem = 'Chưa nhập nội dung câu hỏi';
    options.forEach((o, i) => { if (!o.trim()) e[`options[${i}]`] = `Chưa nhập phương án ${LABELS[i]}`; });
    const seen = new Set<string>();
    options.forEach((o, i) => { const k = o.trim().toLowerCase(); if (k && seen.has(k)) e[`options[${i}]`] = 'Trùng với phương án phía trên'; seen.add(k); });
    if (!subjectId) e.subjectId = 'Chưa chọn môn';
    if (!topicId) e.topicId = 'Chưa chọn chủ đề';
    return e;
  };

  const save = async () => {
    const local = validate();
    setErrors(local); setBanner(null); setConflict(false);
    if (Object.keys(local).length) return;
    setSaving(true);
    try {
      if (isMock) {
        const subject = catalog.find(s => s.id === subjectId)!;
        const topic = subject.topics.find(t => t.id === topicId)!;
        const now = new Date().toISOString();
        const revision = (initial?.revision ?? 0) + 1;
        const saved: QuestionDetail = {
          id: initial?.id ?? `q-${Date.now()}`, revision, status: 'DRAFT', origin: 'MANUAL', stem: stem.trim(), options: options.map(o => o.trim()),
          correctOption: LABELS[correct], subject: { id: subject.id, name: subject.name }, topic: { id: topic.id, name: topic.name },
          difficulty, source: source.trim() || null, ownerUserId: initial?.ownerUserId ?? 'mock-user', updatedAt: now,
          revisions: [{ revision, status: 'DRAFT', origin: 'MANUAL', createdByUserId: 'mock-user', createdAt: now }, ...(initial?.revisions ?? [])],
        };
        onSaved(saved);
        return;
      }

      const res = await fetch(isNew ? '/api/v1/questions' : `/api/v1/questions/${initial.id}`, {
        method: isNew ? 'POST' : 'PUT',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', ...(csrfToken ? { 'X-CSRF-TOKEN': csrfToken } : {}) },
        body: JSON.stringify({
          stem, options, correctOption: LABELS[correct], difficulty, subjectId, topicId,
          source: source.trim() || null, baseRevision: isNew ? undefined : initial.revision,
        }),
      });
      if (res.ok) { onSaved(); return; }

      const problem = await res.json().catch(() => null) as { errors?: ServerError[]; title?: string } | null;
      if (res.status === 400 && problem?.errors?.length) {
        setErrors(Object.fromEntries(problem.errors.map(e => [e.field, e.message])));
        setBanner('Có trường chưa hợp lệ, hãy kiểm tra lại');
      } else if (res.status === 409) { setConflict(true); setBanner(problem?.title ?? 'Câu hỏi đã được sửa ở revision mới hơn. Hãy tải lại trước khi lưu.'); }
      else if (res.status === 403) setBanner('Bạn không có quyền sửa câu hỏi này');
      else if (res.status === 401) setBanner('Phiên đăng nhập đã hết hạn, hãy đăng nhập lại');
      else setBanner('Không thể lưu câu hỏi');
    } catch {
      setBanner('Không kết nối được máy chủ');
    } finally { setSaving(false); }
  };

  const fieldError = (f: string) => errors[f] && <div className="form-error" role="alert">⚠ {errors[f]}</div>;

  return (
    <>
      <div className="modal-header">
        <div>
          <div className="modal-title">{isNew ? 'Thêm câu hỏi' : `Sửa ${initial.id}`}</div>
          <div className="modal-subtitle">{isNew ? 'Câu hỏi mới sẽ ở trạng thái nháp' : `Lưu sẽ tạo revision ${initial.revision + 1} ở trạng thái nháp — revision cũ được giữ nguyên`}</div>
        </div>
        <button className="modal-close" onClick={onClose} id="btn-close-question" aria-label="Đóng">✕</button>
      </div>
      <div className="modal-body">
        {banner && (
          <div role="alert" style={{ background: 'var(--danger-50)', border: '1px solid var(--danger-200)', borderRadius: 8, padding: '10px 14px', fontSize: 13, color: 'var(--danger-700)', marginBottom: 12 }}>
            ⚠ {banner}{conflict && <> <button className="btn btn-secondary btn-sm" onClick={onReload} id="btn-reload-question">Tải lại</button></>}
          </div>
        )}
        <div className="form-group">
          <label className="form-label" htmlFor="q-content">Nội dung <span className="required">*</span></label>
          <textarea id="q-content" className="form-input" rows={3} maxLength={4000} value={stem} onChange={e => { setStem(e.target.value); clear('stem'); }} style={{ resize: 'vertical' }} autoFocus />
          <div className="form-hint">{stem.length} / 4000</div>
          {fieldError('stem')}
        </div>
        <label className="form-label">Phương án (chọn đáp án đúng) <span className="required">*</span></label>
        {fieldError('correctOption')}
        <div style={{ display: 'grid', gap: 8, marginBottom: 16 }}>
          {options.map((o, i) => (
            <div key={i}>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <button type="button" onClick={() => { setCorrect(i); clear('correctOption'); }} id={`q-correct-${i}`} aria-label={`Đáp án ${LABELS[i]} đúng`} aria-pressed={correct === i}
                  style={{ width: 32, height: 32, borderRadius: 8, border: 'none', cursor: 'pointer', fontWeight: 700, flexShrink: 0,
                    background: correct === i ? 'var(--accent-600)' : 'var(--gray-100)', color: correct === i ? 'white' : 'var(--gray-600)' }}>
                  {correct === i ? <Check size={15} /> : LABELS[i]}
                </button>
                <input className="form-input" value={o} id={`q-option-${i}`} maxLength={1000} placeholder={`Phương án ${LABELS[i]}`}
                  onChange={e => { const next = [...options]; next[i] = e.target.value; setOptions(next); clear(`options[${i}]`); }} />
              </div>
              {fieldError(`options[${i}]`)}
            </div>
          ))}
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
          <div className="form-group"><label className="form-label" htmlFor="q-subject">Môn <span className="required">*</span></label>
            <select id="q-subject" className="form-select" value={subjectId} disabled={!isNew} onChange={e => { setSubjectId(e.target.value); setTopicId(''); clear('subjectId'); }}>
              <option value="">Chọn môn</option>{catalog.filter(s => s.canWrite || s.id === subjectId).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
            </select>{fieldError('subjectId')}</div>
          <div className="form-group"><label className="form-label" htmlFor="q-topic">Chủ đề <span className="required">*</span></label>
            <select id="q-topic" className="form-select" value={topicId} disabled={!subjectId} onChange={e => { setTopicId(e.target.value); clear('topicId'); }}>
              <option value="">Chọn chủ đề</option>{topics.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>{fieldError('topicId')}</div>
          <div className="form-group"><label className="form-label" htmlFor="q-diff">Độ khó <span className="required">*</span></label>
            <select id="q-diff" className="form-select" value={difficulty} onChange={e => { setDifficulty(e.target.value as Difficulty); clear('difficulty'); }}>
              {(Object.keys(DIFF) as Difficulty[]).map(d => <option key={d} value={d}>{DIFF[d][0]}</option>)}
            </select>{fieldError('difficulty')}</div>
        </div>
        <div className="form-group"><label className="form-label" htmlFor="q-source">Nguồn (tùy chọn)</label>
          <input id="q-source" className="form-input" maxLength={2000} value={source} onChange={e => { setSource(e.target.value); clear('source'); }} placeholder="Ví dụ: SGK Toán 12, trang 12" />
          {fieldError('source')}</div>
      </div>
      <div className="modal-footer">
        <button className="btn btn-secondary" onClick={onClose} id="btn-cancel-question">Hủy</button>
        <button className={clsx('btn btn-primary', saving && 'btn-loading')} disabled={saving} onClick={save} id="btn-save-draft">Lưu nháp</button>
      </div>
    </>
  );
}

function HistoryModal({ id, isMock, mockStore, onClose }: { id: string; isMock: boolean; mockStore: QuestionDetail[]; onClose: () => void }) {
  const { detail, error, loading, reload } = useDetail(id, isMock, mockStore);
  return (
    <div className="modal-overlay" onClick={e => e.target === e.currentTarget && onClose()}>
      <div className="modal">
        <div className="modal-header">
          <div><div className="modal-title">Lịch sử revision</div><div className="modal-subtitle">{id} · mỗi revision là bất biến</div></div>
          <button className="modal-close" onClick={onClose} id="btn-close-history" aria-label="Đóng">✕</button>
        </div>
        <div className="modal-body">
          {loading && <div className="skeleton" style={{ height: 120, borderRadius: 8 }} />}
          {error && <div role="alert">⚠ {error} <button className="btn btn-secondary btn-sm" onClick={reload} id="btn-retry-history">Thử lại</button></div>}
          {detail?.revisions.map((r, idx) => (
            <div key={r.revision} style={{ display: 'flex', gap: 14, paddingBottom: 16 }}>
              <div style={{ width: 28, height: 28, borderRadius: 99, flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 700,
                background: idx === 0 ? 'var(--brand-600)' : 'var(--gray-100)', color: idx === 0 ? 'white' : 'var(--gray-500)' }}>{r.revision}</div>
              <div>
                <div style={{ fontWeight: 600, fontSize: 13.5 }}>
                  Revision {r.revision} {idx === 0 && <span className="badge badge-blue" style={{ marginLeft: 6 }}>Hiện tại</span>}
                  <span className={clsx('badge', STATUS[r.status][1])} style={{ marginLeft: 6 }}>{STATUS[r.status][0]}</span>
                </div>
                <div style={{ fontSize: 12.5, color: 'var(--gray-500)' }}>{ORIGIN[r.origin]} · {formatDateTime(r.createdAt)}</div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
