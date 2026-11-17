'use client';

<<<<<<< HEAD
import React, { useEffect, useMemo, useState, useCallback } from 'react';
import {
  BookOpen, Plus, Search, RefreshCw, Pencil,
  Trash2, Check, Send, BookMarked, Lock, ChevronLeft, ChevronRight, X
} from 'lucide-react';
=======
import React, { useEffect, useMemo, useState } from 'react';
import { BookOpen, Plus, Search, RefreshCw, Database, Pencil, History, Check } from 'lucide-react';
>>>>>>> origin/dev
import clsx from 'clsx';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
<<<<<<< HEAD
import { formatDateTime } from '@/lib/fetchOrMock';
import {
  BankQuestion,
  Difficulty,
  QuestionSource,
  QuestionStatus,
  useWorkflowStore,
  upsertBankQuestion,
  submitDraftQuestionForReview,
  deleteDraftQuestionFromStore,
  setWorkflowNavIntent,
} from '@/lib/workflowStore';

export const DIFF: Record<Difficulty, [string, string]> = {
  EASY: ['Dễ', 'badge-green'],
  MEDIUM: ['Trung bình', 'badge-amber'],
  HARD: ['Khó', 'badge-red'],
};
=======
import { useAuth } from '@/contexts/AuthContext';
import { ApiError, fetchOrMock, formatDateTime } from '@/lib/fetchOrMock';
import QuestionHistoryModal from './QuestionHistoryModal';
import { DIFF, STATUS, type Difficulty, type Origin, type QStatus, type Ref, type RevisionDetail } from './questionLabels';

/* ============================================================
   TYPES
   ============================================================ */

interface CatalogTopic { id: string; code: string; name: string }
interface CatalogSubject { id: string; code: string; name: string; canWrite: boolean; topics: CatalogTopic[] }

interface QuestionItem {
  id: string; revision: number; status: QStatus; stem: string;
  subject: Ref; topic: Ref; difficulty: Difficulty; ownerUserId: string; updatedAt: string;
}
interface RevisionInfo { revision: number; status: QStatus; origin: Origin; createdByUserId: string; createdAt: string }
interface QuestionDetail extends QuestionItem {
  origin: Origin; options: string[]; correctOption: string; source: string | null; revisions: RevisionInfo[];
  /** Sample data only: the full content of every revision, which the real API serves from /revisions. */
  snapshots?: RevisionDetail[];
}
interface Paged { items: QuestionItem[]; page: number; pageSize: number; total: number }
interface Filters { subjectId: string; topicId: string; difficulty: '' | Difficulty; status: '' | QStatus; text: string }
interface ServerError { code: string; field: string; message: string }
interface ListResult { key: string; data: Paged | null; error: string | null }

const PAGE_SIZE = 20;
const LABELS = 'ABCD';
const NO_FILTERS: Filters = { subjectId: '', topicId: '', difficulty: '', status: '', text: '' };

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
  snapshots: [{
    revision: 1, status, origin: 'MANUAL', stem, options, correctOption, topic, difficulty, source: null,
    importBatchId: null, importRowNumber: null, createdByUserId: 'mock-user', createdAt: updatedAt, isLatest: true, isApprovedInUse: false,
  }],
});

const MOCK_QUESTIONS: QuestionDetail[] = [
  mockQuestion('q-1001', { id: 'sub-math', name: 'Toán' }, { id: 'top-deriv', name: 'Ứng dụng đạo hàm để khảo sát hàm số' }, 'EASY', 'DRAFT',
    'Hàm số y = x³ − 3x + 2 đồng biến trên khoảng nào?', ['(−∞; −1) và (1; +∞)', '(−1; 1)', '(−∞; 1)', '(−1; +∞)'], 'A', '2026-10-17T01:10:00Z'),
  mockQuestion('q-1002', { id: 'sub-math', name: 'Toán' }, { id: 'top-integral', name: 'Nguyên hàm và tích phân' }, 'EASY', 'APPROVED',
    'Giá trị của tích phân ∫₀¹ 2x dx bằng bao nhiêu?', ['0', '1', '2', '1/2'], 'B', '2026-10-16T09:40:00Z'),
  mockQuestion('q-1003', { id: 'sub-phys', name: 'Vật lý' }, { id: 'top-gas', name: 'Khí lí tưởng' }, 'EASY', 'PENDING_REVIEW',
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
>>>>>>> origin/dev

const STATUS: Record<QuestionStatus, [string, string]> = {
  DRAFT: ['Bản nháp', 'badge-gray'],
  REVIEW_REQUIRED: ['Chờ duyệt', 'badge-amber'],
  APPROVED: ['Đã duyệt', 'badge-green'],
  REJECTED: ['Trả về cần sửa', 'badge-red'],
};

<<<<<<< HEAD
const SOURCE_META: Record<QuestionSource, [string, string]> = {
  MANUAL: ['Soạn tay', 'badge-gray'],
  IMPORT: ['Bảng CSV', 'badge-teal'],
  AI_DRAFT: ['Gợi ý AI', 'badge-amber'],
  SEED: ['Khởi tạo', 'badge-gray'],
};

const PAGE_SIZE = 6;

export default function QuestionsPage({ onNavigate }: { onNavigate?: (href: string) => void }) {
  const { user, primaryRole, csrfToken } = useAuth();
  const { success, error: showError } = useToast();
  const wf = useWorkflowStore();

  const [tab, setTab] = useState<'questions' | 'corpus'>('questions');
  const [apiItems, setApiItems] = useState<BankQuestion[] | null>(null);
  const [loading, setLoading] = useState(true);

  const [search, setSearch] = useState('');
  const [subject, setSubject] = useState(() => wf.navIntent?.subject ?? '');
  const [topicFilter, setTopicFilter] = useState(() => wf.navIntent?.topic ?? '');
  const [difficulty, setDifficulty] = useState<'' | Difficulty>(() => wf.navIntent?.difficulty ?? '');
  const [status, setStatus] = useState<'' | QuestionStatus>('');
  const [sourceFilter, setSourceFilter] = useState<'' | QuestionSource>('');
  const [page, setPage] = useState(1);

  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [editing, setEditing] = useState<BankQuestion | null>(null);

  const canAuthor = primaryRole === 'Teacher' || primaryRole === 'DepartmentHead';

  const fetchQuestions = useCallback(async () => {
    try {
      const res = await fetch('/api/v1/questions', { credentials: 'include' });
      if (res.ok) {
        const data = (await res.json()) as BankQuestion[];
        if (Array.isArray(data)) {
          setApiItems(data);
          return;
        }
      }
      setApiItems(null);
    } catch {
      setApiItems(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let active = true;
    const run = async () => {
      await fetchQuestions();
      if (!active) return;
    };
    void run();
    return () => {
      active = false;
    };
  }, [fetchQuestions]);

  const items = apiItems ?? wf.questions;
  const isMock = apiItems === null;

  const subjects = useMemo(() => Array.from(new Set(items.map(i => i.subject))), [items]);
  const topics = useMemo(
    () =>
      Array.from(
        new Set(items.filter(i => !subject || i.subject === subject).map(i => i.topic)),
      ),
    [items, subject],
  );

  const filtered = useMemo(
    () =>
      items.filter(
        i =>
          (!search ||
            `${i.content} ${i.topic} ${i.id} ${i.author}`
              .toLowerCase()
              .includes(search.toLowerCase())) &&
          (!subject || i.subject === subject) &&
          (!topicFilter || i.topic.toLowerCase() === topicFilter.toLowerCase()) &&
          (!difficulty || i.difficulty === difficulty) &&
          (!status || i.status === status) &&
          (!sourceFilter || i.source === sourceFilter),
      ),
    [items, search, subject, topicFilter, difficulty, status, sourceFilter],
  );

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);
  const pagedItems = filtered.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);
  const activeQuestion =
    items.find(q => q.id === selectedId) ?? pagedItems[0] ?? filtered[0] ?? null;

  const stats = (['APPROVED', 'REVIEW_REQUIRED', 'DRAFT', 'REJECTED'] as QuestionStatus[]).map(s => ({
    s,
    n: items.filter(i => i.status === s).length,
  }));

  const handleSaveQuestion = async (
    draft: {
      id?: string;
      content: string;
      options: string[];
      correctIndex: number;
      explanation?: string;
      subject: string;
      topic: string;
      difficulty: Difficulty;
      points: number;
    },
    submitForReview: boolean,
  ) => {
    const actor = user?.userName ?? 'gv.nguyen.van.a';
    if (!isMock && csrfToken) {
      try {
        const res = await fetch(
          draft.id ? `/api/v1/questions/${encodeURIComponent(draft.id)}` : '/api/v1/questions',
          {
            method: draft.id ? 'PUT' : 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken },
            body: JSON.stringify({ ...draft, submitForReview }),
          },
        );
        if (!res.ok) {
          showError(`Máy chủ từ chối lưu câu hỏi (HTTP ${res.status})`);
          return;
        }
        await fetchQuestions();
        setEditing(null);
        success(submitForReview ? 'Đã gửi duyệt câu hỏi' : 'Đã lưu bản nháp');
        return;
      } catch {
        showError('Lỗi kết nối máy chủ');
        return;
      }
    }

    const updated = upsertBankQuestion({
      ...draft,
      actorUserName: actor,
      submitForReview,
    });
    setSelectedId(updated.id);
    setEditing(null);
    success(
      submitForReview
        ? `Đã tạo phiên bản ${updated.revision} (v${updated.version}) và gửi thẩm định`
        : `Đã lưu bản nháp ${updated.id} (rev ${updated.revision})`,
    );
  };

  const handleQuickSubmit = (q: BankQuestion) => {
    const actor = user?.userName ?? 'gv.nguyen.van.a';
    if (submitDraftQuestionForReview(q.id, actor)) {
      success(`Đã gửi ${q.id} (rev ${q.revision}) sang Bàn duyệt câu hỏi`);
    }
  };

  const clearSlotIntent = () => {
    setWorkflowNavIntent(null);
    setSubject('');
    setTopicFilter('');
    setDifficulty('');
    setPage(1);
=======
export default function QuestionsPage() {
  const { success, error: showError } = useToast();
  const { user, apiFetch, csrfToken } = useAuth();
  const [catalog, setCatalog] = useState<CatalogSubject[]>([]);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [catalogTick, setCatalogTick] = useState(0);
  const [mockStore, setMockStore] = useState<QuestionDetail[]>(MOCK_QUESTIONS);
  const [result, setResult] = useState<ListResult | null>(null);
  const [isMock, setIsMock] = useState(false);
  const [reloadTick, setReloadTick] = useState(0);
  const [filters, setFilters] = useState<Filters>(NO_FILTERS);
  const [searchInput, setSearchInput] = useState('');
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<string | 'new' | null>(null);
  const [history, setHistory] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetchOrMock<CatalogSubject[]>('/api/v1/catalog', MOCK_CATALOG, apiFetch)
      .then(r => { if (!cancelled) { setCatalog(r.data); setCatalogError(null); } })
      // Keep the last good catalog: a failed refresh must not take the subject/topic choices away.
      .catch(e => { if (!cancelled) setCatalogError(describe(e, 'tải danh mục môn học và chủ đề')); });
    return () => { cancelled = true; };
  }, [apiFetch, catalogTick]);
  const retryCatalog = () => { setCatalogError(null); setCatalogTick(t => t + 1); };

  useEffect(() => {
    const timer = setTimeout(() => { setFilters(f => f.text === searchInput ? f : { ...f, text: searchInput }); setPage(1); }, 300);
    return () => clearTimeout(timer);
  }, [searchInput]);

  const query = useMemo(() => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) });
    if (filters.subjectId) params.set('subjectId', filters.subjectId);
    if (filters.topicId) params.set('topicId', filters.topicId);
    if (filters.difficulty) params.set('difficulty', filters.difficulty);
    if (filters.status) params.set('status', filters.status);
    if (filters.text.trim()) params.set('q', filters.text.trim());
    return params.toString();
  }, [filters, page]);
  const requestKey = `${query}#${reloadTick}`;

  useEffect(() => {
    // Cleanup drops the response of a superseded request, so an older, slower one cannot overwrite the page.
    let cancelled = false;
    fetchOrMock<Paged>(`/api/v1/questions?${query}`, mockPage(mockStore, filters, page), apiFetch)
      .then(r => { if (!cancelled) { setResult({ key: requestKey, data: r.data, error: null }); setIsMock(r.isMock); } })
      .catch(e => { if (!cancelled) setResult({ key: requestKey, data: null, error: describe(e, 'tải danh sách câu hỏi') }); });
    return () => { cancelled = true; };
  }, [query, requestKey, filters, page, mockStore, apiFetch]);

  const load = () => setReloadTick(t => t + 1);
  const loading = result?.key !== requestKey;
  const data = result?.data ?? null;
  const error = loading ? null : result?.error ?? null;

  const topics = catalog.find(s => s.id === filters.subjectId)?.topics ?? [];
  const setFilter = (patch: Partial<Filters>) => { setFilters(f => ({ ...f, ...patch })); setPage(1); };
  const clearFilters = () => { setFilters(NO_FILTERS); setSearchInput(''); setPage(1); };
  const hasFilters = Object.values(filters).some(Boolean);
  // A department head sees subjects to filter by them but cannot write; editing is for the owner outside review.
  const canCreate = isMock || catalog.some(s => s.canWrite);
  const canEdit = (q: QuestionItem) =>
    q.status !== 'PENDING_REVIEW' && (isMock || (!!user && q.ownerUserId.toLowerCase() === user.id.toLowerCase()));
  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  const onSaved = (mockDetail?: QuestionDetail) => {
    if (mockDetail) setMockStore(prev => prev.some(q => q.id === mockDetail.id) ? prev.map(q => q.id === mockDetail.id ? mockDetail : q) : [mockDetail, ...prev]);
    setEditing(null); load();
>>>>>>> origin/dev
  };

  // Resolves true when the server accepted the submission, so a caller (the history modal) can refresh itself.
  const submitRevision = async (id: string, revision: number): Promise<boolean> => {
    let accepted = false;
    try {
      // Bản nháp mới có review version 1; server kiểm tra cả revision lẫn version.
      // Nếu câu đã đổi, server trả 409 và danh sách được tải lại.
      const response = await apiFetch(`/api/v1/questions/${id}/submit-review`, {
        method: 'POST', credentials: 'include',
        headers: { 'Content-Type': 'application/json', ...(csrfToken ? { 'X-CSRF-TOKEN': csrfToken } : {}) },
        body: JSON.stringify({ revision, expectedVersion: 1 }),
      });
      accepted = response.ok;
      if (response.ok) success('Đã gửi câu hỏi để duyệt');
      else if (response.status === 409) showError('Câu hỏi đã thay đổi. Hãy kiểm tra lại');
      else showError('Không gửi duyệt được câu hỏi');
    } catch { showError('Không kết nối được máy chủ'); }
    load();
    return accepted;
  };
  const submitForReview = (q: QuestionItem) => submitRevision(q.id, q.revision);
  // Same rule as the button in the list: only the owner, and only for a real API.
  const historyOwner = history ? data?.items.find(q => q.id === history)?.ownerUserId : undefined;
  const canSubmitFromHistory = !isMock && !!user && !!historyOwner && historyOwner.toLowerCase() === user.id.toLowerCase();

  return (
    <div>
      <div className="page-header">
<<<<<<< HEAD
        <div>
          <h1 className="page-title">Mục lục câu hỏi &amp; Lịch sử phiên bản</h1>
          <p className="page-subtitle">
            Tra cứu theo chủ đề, độ khó, nguồn và trạng thái; bấm chọn một câu để mở phương án, đáp án và các phiên bản ngay cùng màn hình
          </p>
        </div>
        <div className="page-actions">
          <button
            className="btn btn-secondary"
            onClick={() => {
              setLoading(true);
              void fetchQuestions();
            }}
            id="btn-refresh-questions"
          >
            <RefreshCw size={14} />
          </button>
          {canAuthor && (
            <button
              className="btn btn-primary"
              onClick={() =>
                setEditing(
                  blankQuestion(
                    user?.userName ?? 'gv.nguyen.van.a',
                    subject || 'Toán',
                    topicFilter || '',
                    difficulty || 'MEDIUM',
                  ),
                )
              }
              id="btn-create-question"
            >
              <Plus size={14} />
              Soạn câu hỏi mới
            </button>
          )}
        </div>
      </div>

      <div className="tabs">
        <div
          className={clsx('tab-item', tab === 'questions' && 'active')}
          onClick={() => setTab('questions')}
          id="tab-question-list"
        >
          Mục lục câu hỏi ({items.length})
        </div>
        <div
          className={clsx('tab-item', tab === 'corpus' && 'active')}
          onClick={() => setTab('corpus')}
          id="tab-knowledge-corpus"
        >
          Danh mục Sách giáo khoa tham chiếu ({wf.knowledgeDocs.length})
        </div>
      </div>

      {tab === 'corpus' ? (
        <div className="table-container">
          <div className="table-toolbar">
            <div>
              <strong style={{ fontFamily: 'var(--font-serif)', fontSize: 15 }}>
                Danh mục Sách giáo khoa &amp; Tài liệu chuẩn phục vụ đối chiếu
              </strong>
              <div style={{ fontSize: 12, color: 'var(--ink-600)' }}>
                Chỉ cán bộ và giáo viên thuộc đúng đơn vị trường, tổ bộ môn mới được phép truy xuất nguồn học liệu tương ứng
              </div>
            </div>
          </div>
          <div style={{ overflowX: 'auto' }}>
            <table>
              <thead>
                <tr>
                  <th>Mã tài liệu</th>
                  <th>Tên học liệu chuẩn</th>
                  <th>Môn / Chủ đề</th>
                  <th>Phạm vi đơn vị</th>
                  <th>Số mục chỉ mục</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {wf.knowledgeDocs.map(doc => (
                  <tr key={doc.sourceId}>
                    <td>
                      <code>{doc.sourceId}</code>
                    </td>
                    <td style={{ fontWeight: 600, color: 'var(--ink-900)', fontFamily: 'var(--font-serif)' }}>
                      {doc.title}
                    </td>
                    <td>
                      {doc.subject} / {doc.topic}
                    </td>
                    <td>
                      <span className="badge badge-gray">{doc.scopeLabel}</span>
                    </td>
                    <td className="mono-num">{doc.chunkCount} mục</td>
                    <td>
                      <span className="badge badge-green">Sẵn sàng tra cứu</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : (
        <>
          {(topicFilter || wf.navIntent) && (
            <div
              className="margin-note green-ink"
              style={{
                marginBottom: 14,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                gap: 12,
                flexWrap: 'wrap',
              }}
            >
              <div>
                Đang mở mục lục theo ô ma trận:{' '}
                <strong>
                  {subject || 'Mọi môn'} · {topicFilter || 'Mọi chủ đề'} ·{' '}
                  {difficulty ? DIFF[difficulty][0] : 'Mọi mức độ'}
                </strong>
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                {onNavigate && canAuthor && (
                  <button
                    className="btn btn-primary btn-sm"
                    onClick={() => onNavigate('/generate')}
                  >
                    Yêu cầu AI gợi ý cho ô này
                  </button>
                )}
                <button className="btn btn-secondary btn-sm" onClick={clearSlotIntent}>
                  <X size={13} />
                  Bỏ lọc
                </button>
              </div>
            </div>
          )}

          {/* Compact Filter Bar */}
          <div
            style={{
              display: 'flex',
              gap: 8,
              flexWrap: 'wrap',
              marginBottom: 14,
              alignItems: 'center',
            }}
          >
            <button
              type="button"
              className={clsx('btn btn-sm', status === '' ? 'btn-primary' : 'btn-secondary')}
              onClick={() => {
                setStatus('');
                setPage(1);
              }}
            >
              Tất cả ({items.length})
            </button>
            {stats.map(({ s, n }) => (
              <button
                key={s}
                type="button"
                id={`stat-${s}`}
                className={clsx('btn btn-sm', status === s ? 'btn-primary' : 'btn-secondary')}
                onClick={() => {
                  setStatus(status === s ? '' : s);
                  setPage(1);
                }}
              >
                <span>{STATUS[s][0]}</span>
                <span className="mono-num">({n})</span>
              </button>
            ))}
          </div>

          {/* Master-Detail Split Layout: Index Left, Inline Detail & Revision History Right */}
          <div
            className="questions-index-split"
            style={{
              display: 'grid',
              gridTemplateColumns: '1.2fr 420px',
              gap: 18,
              alignItems: 'start',
            }}
          >
            {/* Left: Document Index Table */}
            <div className="table-container">
              <div className="table-toolbar" style={{ flexWrap: 'wrap', gap: 8 }}>
                <div className="search-bar" style={{ width: 210 }}>
                  <Search size={13} className="search-bar-icon" />
                  <input
                    placeholder="Tìm nội dung, chủ đề, mã…"
                    value={search}
                    onChange={e => {
                      setSearch(e.target.value);
                      setPage(1);
                    }}
                    id="question-search"
                  />
                </div>

                <select
                  className="form-select"
                  style={{ width: 120 }}
                  value={subject}
                  onChange={e => {
                    setSubject(e.target.value);
                    setTopicFilter('');
                    setPage(1);
                  }}
                  id="filter-subject"
                  aria-label="Môn học"
                >
                  <option value="">Mọi môn</option>
                  {subjects.map(s => (
                    <option key={s} value={s}>
                      {s}
                    </option>
                  ))}
                </select>

                <select
                  className="form-select"
                  style={{ width: 135 }}
                  value={topicFilter}
                  onChange={e => {
                    setTopicFilter(e.target.value);
                    setPage(1);
                  }}
                  id="filter-topic"
                  aria-label="Chủ đề"
                >
                  <option value="">Mọi chủ đề</option>
                  {topics.map(t => (
                    <option key={t} value={t}>
                      {t}
                    </option>
                  ))}
                </select>

                <select
                  className="form-select"
                  style={{ width: 125 }}
                  value={difficulty}
                  onChange={e => {
                    setDifficulty(e.target.value as Difficulty | '');
                    setPage(1);
                  }}
                  id="filter-difficulty"
                  aria-label="Độ khó"
                >
                  <option value="">Mọi độ khó</option>
                  {(Object.keys(DIFF) as Difficulty[]).map(d => (
                    <option key={d} value={d}>
                      {DIFF[d][0]}
                    </option>
                  ))}
                </select>

                <select
                  className="form-select"
                  style={{ width: 125 }}
                  value={sourceFilter}
                  onChange={e => {
                    setSourceFilter(e.target.value as QuestionSource | '');
                    setPage(1);
                  }}
                  id="filter-source"
                  aria-label="Nguồn"
                >
                  <option value="">Mọi nguồn</option>
                  {(Object.keys(SOURCE_META) as QuestionSource[]).map(src => (
                    <option key={src} value={src}>
                      {SOURCE_META[src][0]}
                    </option>
                  ))}
                </select>
              </div>

              {loading ? (
                <div style={{ padding: 14 }}>
                  {[0, 1, 2, 3].map(i => (
                    <div key={i} className="skeleton" style={{ height: 42, marginBottom: 8 }} />
                  ))}
                </div>
              ) : filtered.length === 0 ? (
                <div className="table-empty">
                  <div className="table-empty-icon">
                    <BookOpen />
                  </div>
                  <div className="table-empty-text">Không có câu hỏi phù hợp trong mục lục</div>
                </div>
              ) : (
                <>
                  <div style={{ overflowX: 'auto' }}>
                    <table>
                      <thead>
                        <tr>
                          <th>Chủ đề / Nội dung câu hỏi</th>
                          <th>Độ khó</th>
                          <th>Nguồn</th>
                          <th>Trạng thái</th>
                          <th style={{ width: 85 }}>Thao tác</th>
                        </tr>
                      </thead>
                      <tbody>
                        {pagedItems.map(q => {
                          const isSelected = activeQuestion?.id === q.id;
                          return (
                            <tr
                              key={q.id}
                              onClick={() => setSelectedId(q.id)}
                              style={{
                                cursor: 'pointer',
                                background: isSelected ? '#EBF3F1' : undefined,
                              }}
                            >
                              <td style={{ maxWidth: 310 }}>
                                <div style={{ fontSize: 11.5, color: 'var(--ink-500)', marginBottom: 2 }}>
                                  <strong>{q.subject} · {q.topic}</strong> ·{' '}
                                  <code>
                                    {q.id} (rev {q.revision})
                                  </code>
                                </div>
                                <div
                                  style={{
                                    fontWeight: 600,
                                    color: 'var(--ink-900)',
                                    overflow: 'hidden',
                                    textOverflow: 'ellipsis',
                                    whiteSpace: 'nowrap',
                                  }}
                                >
                                  {q.content}
                                </div>
                              </td>
                              <td>
                                <span className={clsx('badge', DIFF[q.difficulty][1])}>
                                  {DIFF[q.difficulty][0]}
                                </span>
                              </td>
                              <td>
                                <span className={clsx('badge', SOURCE_META[q.source][1])}>
                                  {SOURCE_META[q.source][0]}
                                </span>
                              </td>
                              <td>
                                <span className={clsx('badge', STATUS[q.status][1])}>
                                  {STATUS[q.status][0]}
                                </span>
                              </td>
                              <td onClick={e => e.stopPropagation()}>
                                <div style={{ display: 'flex', gap: 4 }}>
                                  {canAuthor && (
                                    <button
                                      className="topbar-icon-btn"
                                      onClick={() => setEditing(q)}
                                      aria-label="Sửa câu hỏi"
                                      id={`btn-edit-${q.id}`}
                                    >
                                      <Pencil size={13} />
                                    </button>
                                  )}
                                  {canAuthor && q.status === 'DRAFT' && (
                                    <>
                                      <button
                                        className="topbar-icon-btn"
                                        onClick={() => handleQuickSubmit(q)}
                                        aria-label="Gửi duyệt"
                                        id={`btn-send-${q.id}`}
                                        style={{ color: 'var(--brand-600)' }}
                                      >
                                        <Send size={13} />
                                      </button>
                                      <button
                                        className="topbar-icon-btn"
                                        onClick={() => {
                                          deleteDraftQuestionFromStore(q.id);
                                          success('Đã xóa bản nháp');
                                        }}
                                        aria-label="Xóa bản nháp"
                                        id={`btn-del-${q.id}`}
                                      >
                                        <Trash2 size={13} />
                                      </button>
                                    </>
                                  )}
                                </div>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>

                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      padding: '10px 14px',
                      borderTop: '1px solid var(--rule-base)',
                      fontSize: 12.5,
                      color: 'var(--ink-600)',
                    }}
                  >
                    <span className="mono-num">
                      Mục {(currentPage - 1) * PAGE_SIZE + 1}–
                      {Math.min(currentPage * PAGE_SIZE, filtered.length)} / {filtered.length}
                    </span>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <button
                        className="btn btn-secondary btn-sm"
                        disabled={currentPage <= 1}
                        onClick={() => setPage(p => Math.max(1, p - 1))}
                        id="btn-prev-page"
                      >
                        <ChevronLeft size={13} /> Trang trước
                      </button>
                      <span className="mono-num" style={{ padding: '0 6px', fontWeight: 700 }}>
                        {currentPage}/{totalPages}
                      </span>
                      <button
                        className="btn btn-secondary btn-sm"
                        disabled={currentPage >= totalPages}
                        onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                        id="btn-next-page"
                      >
                        Trang sau <ChevronRight size={13} />
                      </button>
                    </div>
                  </div>
                </>
              )}
            </div>

            {/* Right: Inline Question Detail & Immutable Revision History */}
            <div className="card" style={{ position: 'sticky', top: 64 }}>
              {!activeQuestion ? (
                <div className="card-body">
                  <div style={{ fontSize: 13, color: 'var(--ink-500)' }}>
                    Chọn một câu hỏi trong mục lục bên trái để xem phương án, đáp án và lịch sử phiên bản.
                  </div>
                </div>
              ) : (
                <>
                  <div className="card-header">
                    <div>
                      <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--ink-500)', textTransform: 'uppercase' }}>
                        Phiếu chi tiết câu hỏi · {activeQuestion.subject} / {activeQuestion.topic}
                      </div>
                      <div className="card-title">
                        <code>{activeQuestion.id}</code> · Phiên bản rev {activeQuestion.revision} (v{activeQuestion.version})
                      </div>
                    </div>
                    <span className={clsx('badge', STATUS[activeQuestion.status][1])}>
                      {STATUS[activeQuestion.status][0]}
                    </span>
                  </div>

                  <div className="card-body">
                    {activeQuestion.status === 'APPROVED' && (
                      <div className="margin-note green-ink" style={{ marginBottom: 12, padding: '6px 10px' }}>
                        <Lock size={12} style={{ display: 'inline', verticalAlign: 'middle', marginRight: 5 }} />
                        Phiên bản đã duyệt là <strong>bất biến</strong>. Bấm chỉnh sửa sẽ tạo <code>rev {activeQuestion.revision + 1}</code> mới.
                      </div>
                    )}

                    {activeQuestion.rejectReason && (
                      <div className="margin-note red-ink" style={{ marginBottom: 12 }}>
                        <strong>Bút phê trả về:</strong> {activeQuestion.rejectReason}
                      </div>
                    )}

                    <div
                      style={{
                        fontFamily: 'var(--font-serif)',
                        fontSize: 15.5,
                        fontWeight: 600,
                        color: 'var(--ink-900)',
                        marginBottom: 12,
                        lineHeight: 1.5,
                      }}
                    >
                      {activeQuestion.content}
                    </div>

                    <div style={{ display: 'grid', gap: 6, marginBottom: 12 }}>
                      {activeQuestion.options.map((opt, idx) => {
                        const isCorrect = idx === activeQuestion.correctIndex;
                        return (
                          <div
                            key={idx}
                            style={{
                              padding: '6px 10px',
                              borderRadius: 3,
                              border: `1px solid ${
                                isCorrect ? 'var(--brand-600)' : 'var(--rule-base)'
                              }`,
                              background: isCorrect ? 'var(--brand-50)' : '#FFFFFF',
                              fontSize: 13,
                              fontWeight: isCorrect ? 700 : 400,
                              color: isCorrect ? 'var(--brand-700)' : 'var(--ink-800)',
                            }}
                          >
                            <strong>{String.fromCharCode(65 + idx)}.</strong> {opt}
                            {isCorrect && ' (Đáp án đúng)'}
                          </div>
                        );
                      })}
                    </div>

                    {activeQuestion.citation && (
                      <div
                        style={{
                          padding: '8px 10px',
                          background: '#F4F0E5',
                          border: '1px solid var(--rule-base)',
                          borderRadius: 3,
                          fontSize: 12,
                          marginBottom: 12,
                        }}
                      >
                        <BookMarked size={12} style={{ display: 'inline', verticalAlign: 'middle', marginRight: 4, color: 'var(--brand-600)' }} />
                        <strong>Nguồn:</strong> {activeQuestion.citation.source} ({activeQuestion.citation.locator})
                      </div>
                    )}

                    {canAuthor && (
                      <div style={{ display: 'flex', gap: 8, marginBottom: 14 }}>
                        <button
                          className="btn btn-secondary btn-sm"
                          style={{ flex: 1 }}
                          onClick={() => setEditing(activeQuestion)}
                        >
                          <Pencil size={12} />
                          {activeQuestion.status === 'APPROVED'
                            ? `Tạo phiên bản rev ${activeQuestion.revision + 1}`
                            : 'Chỉnh sửa câu hỏi'}
                        </button>
                        {activeQuestion.status === 'DRAFT' && (
                          <button
                            className="btn btn-primary btn-sm"
                            onClick={() => handleQuickSubmit(activeQuestion)}
                          >
                            <Send size={12} />
                            Gửi duyệt
                          </button>
                        )}
                      </div>
                    )}

                    {/* Revision History right inside the detail pane */}
                    <div
                      style={{
                        borderTop: '1px solid var(--rule-base)',
                        paddingTop: 10,
                      }}
                    >
                      <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--ink-500)', textTransform: 'uppercase', marginBottom: 6 }}>
                        Lịch sử phiên bản ({activeQuestion.revisions.length})
                      </div>
                      <div style={{ display: 'grid', gap: 6, maxHeight: 160, overflowY: 'auto' }}>
                        {activeQuestion.revisions.map(rev => (
                          <div
                            key={rev.revision}
                            style={{
                              padding: '6px 9px',
                              background: '#F5F2E8',
                              border: '1px solid var(--rule-light)',
                              borderRadius: 3,
                              fontSize: 12,
                            }}
                          >
                            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                              <strong className="mono-num">
                                Phiên bản {rev.revision} (v{rev.version})
                              </strong>
                              <span style={{ color: 'var(--ink-500)' }}>
                                {formatDateTime(rev.updatedAt)}
                              </span>
                            </div>
                            {rev.note && (
                              <div style={{ color: 'var(--ink-600)', marginTop: 2 }}>
                                {rev.note}
                              </div>
                            )}
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                </>
              )}
            </div>
          </div>
        </>
      )}

      {editing && (
        <QuestionModal
          initial={editing}
          onClose={() => setEditing(null)}
          onSave={handleSaveQuestion}
        />
      )}

      <style>{`
        @media (max-width: 1024px) {
          .questions-index-split { grid-template-columns: 1fr !important; }
        }
      `}</style>
=======
        <h1 className="page-title">Ngân hàng câu hỏi</h1>
        <p className="page-subtitle">Câu hỏi trắc nghiệm theo môn, chủ đề và độ khó; mỗi lần lưu tạo một revision mới ở trạng thái nháp</p>
        <div className="page-actions">
          {isMock && <span className="badge badge-amber"><Database size={12} />Dữ liệu mẫu</span>}
          <button className="btn btn-secondary" onClick={load} id="btn-refresh-questions" aria-label="Làm mới"><RefreshCw size={14} /></button>
          {canCreate && <button className="btn btn-primary" onClick={() => setEditing('new')} id="btn-create-question"><Plus size={14} />Thêm câu hỏi</button>}
        </div>
      </div>

      {catalogError && (
        <div role="alert" style={{ background: 'var(--danger-50)', border: '1px solid var(--danger-200)', borderRadius: 8, padding: '10px 14px', fontSize: 13, color: 'var(--danger-700)', marginBottom: 12 }}>
          ⚠ {catalogError}. Chưa thể lọc theo môn/chủ đề hoặc thêm câu hỏi.{' '}
          <button className="btn btn-secondary btn-sm" onClick={retryCatalog} id="btn-retry-catalog">Thử lại</button>
        </div>
      )}

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
                      {!isMock && q.status === 'DRAFT' && canEdit(q) &&
                        <button className="btn btn-secondary btn-sm" onClick={() => void submitForReview(q)} id={`btn-submit-review-${q.id}`}>Gửi duyệt</button>}
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
          onClose={() => setEditing(null)} onShowHistory={setHistory} onSaved={d => { success(editing === 'new' ? 'Đã lưu câu hỏi ở trạng thái nháp' : 'Đã lưu revision mới ở trạng thái nháp'); onSaved(d); }} />
      )}
      {history && (
        <QuestionHistoryModal id={history} onClose={() => setHistory(null)}
          onSubmitReview={canSubmitFromHistory ? revision => submitRevision(history, revision) : undefined}
          mockRevisions={isMock ? mockStore.find(q => q.id === history)?.snapshots ?? [] : null} />
      )}
>>>>>>> origin/dev
    </div>
  );
}

<<<<<<< HEAD
function blankQuestion(
  author: string,
  subject = 'Toán',
  topic = '',
  difficulty: Difficulty = 'MEDIUM',
): BankQuestion {
  return {
    id: '',
    revision: 0,
    version: 0,
    content: '',
    options: ['', '', '', ''],
    correctIndex: 0,
    explanation: '',
    subject,
    topic,
    difficulty,
    points: 0.25,
    status: 'DRAFT',
    source: 'MANUAL',
    ownerUserName: author,
    author,
    updatedAt: new Date().toISOString(),
    revisions: [],
    decisions: [],
  };
}

function QuestionModal({
  initial,
  onClose,
  onSave,
}: {
  initial: BankQuestion;
  onClose: () => void;
  onSave: (
    draft: {
      id?: string;
      content: string;
      options: string[];
      correctIndex: number;
      explanation?: string;
      subject: string;
      topic: string;
      difficulty: Difficulty;
      points: number;
    },
    submitForReview: boolean,
  ) => void;
}) {
  const [q, setQ] = useState(initial);
  const [err, setErr] = useState('');
  const isNew = !initial.id || initial.revision === 0;

  const submit = (send: boolean) => {
    if (!q.content.trim()) return setErr('Chưa nhập nội dung câu hỏi');
    if (q.options.some(o => !o.trim())) return setErr('Cần nhập đủ 4 phương án A, B, C, D');
    if (new Set(q.options.map(o => o.trim().toLowerCase())).size !== 4)
      return setErr('4 phương án A, B, C, D không được trùng nhau');
    if (!q.subject.trim()) return setErr('Chưa nhập môn học');
    if (!q.topic.trim()) return setErr('Chưa nhập chủ đề');
    if (!(q.points > 0)) return setErr('Điểm số phải lớn hơn 0');
    onSave(
      {
        id: isNew ? undefined : q.id,
        content: q.content.trim(),
        options: q.options.map(o => o.trim()),
        correctIndex: q.correctIndex,
        explanation: q.explanation?.trim() || undefined,
        subject: q.subject.trim(),
        topic: q.topic.trim(),
        difficulty: q.difficulty,
        points: q.points,
      },
      send,
    );
  };

  return (
    <div className="modal-overlay" onClick={e => e.target === e.currentTarget && onClose()}>
      <div className="modal modal-lg">
        <div className="modal-header">
          <div>
            <div className="modal-title">
              {isNew ? 'Soạn câu hỏi trắc nghiệm mới' : `Hiệu đính câu hỏi ${q.id}`}
            </div>
            <div className="modal-subtitle">
              {isNew
                ? 'Câu hỏi trắc nghiệm 1 đáp án đúng'
                : `Lưu sẽ sinh Phiên bản (Revision) ${q.revision + 1} mới — giữ nguyên các phiên bản trước`}
            </div>
          </div>
          <button className="modal-close" onClick={onClose} id="btn-close-question">
            ✕
          </button>
        </div>
        <div className="modal-body">
          {err && (
            <div role="alert" className="margin-note red-ink" style={{ marginBottom: 12 }}>
              {err}
            </div>
          )}
          <div className="form-group">
            <label className="form-label" htmlFor="q-content">
              Nội dung câu dẫn <span className="required">*</span>
            </label>
            <textarea
              id="q-content"
              className="form-input"
              rows={3}
              value={q.content}
              onChange={e => {
                setQ({ ...q, content: e.target.value });
                setErr('');
              }}
              style={{ resize: 'vertical' }}
              autoFocus
            />
          </div>

          <label className="form-label">
            4 phương án lựa chọn (bấm chữ cái A/B/C/D để đặt đáp án đúng){' '}
            <span className="required">*</span>
          </label>
          <div style={{ display: 'grid', gap: 8, marginBottom: 14 }}>
            {q.options.map((o, i) => (
              <div key={i} style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <button
                  type="button"
                  onClick={() => setQ({ ...q, correctIndex: i })}
                  id={`q-correct-${i}`}
                  aria-label={`Đáp án ${String.fromCharCode(65 + i)} đúng`}
                  className="mono-num"
                  style={{
                    width: 30,
                    height: 30,
                    borderRadius: 3,
                    border: 'none',
                    cursor: 'pointer',
                    fontWeight: 700,
                    flexShrink: 0,
                    background: q.correctIndex === i ? 'var(--brand-600)' : '#EAE5D9',
                    color: q.correctIndex === i ? '#FFFFFF' : 'var(--ink-800)',
                  }}
                >
                  {q.correctIndex === i ? <Check size={14} /> : String.fromCharCode(65 + i)}
                </button>
                <input
                  className="form-input"
                  value={o}
                  id={`q-option-${i}`}
                  placeholder={`Phương án ${String.fromCharCode(65 + i)}`}
                  onChange={e => {
                    const opts = [...q.options];
                    opts[i] = e.target.value;
                    setQ({ ...q, options: opts });
                    setErr('');
                  }}
                />
              </div>
            ))}
          </div>

          <div className="form-group">
            <label className="form-label" htmlFor="q-explanation">
              Lời giải / Ghi chú đáp án
            </label>
            <input
              id="q-explanation"
              className="form-input"
              value={q.explanation ?? ''}
              onChange={e => setQ({ ...q, explanation: e.target.value })}
              placeholder="Giải thích ngắn gọn căn cứ chọn đáp án"
            />
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 10 }}>
            <div className="form-group">
              <label className="form-label" htmlFor="q-subject">
                Môn học
              </label>
              <input
                id="q-subject"
                className="form-input"
                value={q.subject}
                onChange={e => setQ({ ...q, subject: e.target.value })}
              />
            </div>
            <div className="form-group">
              <label className="form-label" htmlFor="q-topic">
                Chủ đề
              </label>
              <input
                id="q-topic"
                className="form-input"
                value={q.topic}
                onChange={e => setQ({ ...q, topic: e.target.value })}
              />
            </div>
            <div className="form-group">
              <label className="form-label" htmlFor="q-diff">
                Độ khó
              </label>
              <select
                id="q-diff"
                className="form-select"
                value={q.difficulty}
                onChange={e => setQ({ ...q, difficulty: e.target.value as Difficulty })}
              >
                {(Object.keys(DIFF) as Difficulty[]).map(d => (
                  <option key={d} value={d}>
                    {DIFF[d][0]}
                  </option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label className="form-label" htmlFor="q-points">
                Điểm
              </label>
              <input
                id="q-points"
                type="number"
                step={0.05}
                min={0.05}
                className="form-input mono-num"
                value={q.points}
                onChange={e => setQ({ ...q, points: Number(e.target.value) })}
              />
            </div>
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn btn-secondary" onClick={onClose} id="btn-cancel-question">
            Hủy
          </button>
          <button className="btn btn-secondary" onClick={() => submit(false)} id="btn-save-draft">
            Lưu bản nháp
          </button>
          <button className="btn btn-primary" onClick={() => submit(true)} id="btn-submit-review">
            Gửi thẩm định
          </button>
=======
/* ============================================================
   DETAIL LOADING (shared by editor and history)
   ============================================================ */

function useDetail(id: string | null, isMock: boolean, mockStore: QuestionDetail[]) {
  const { apiFetch } = useAuth();
  // The store is read once per open; later edits to the sample data must not reset a form in progress.
  const [store] = useState(mockStore);
  const [tick, setTick] = useState(0);
  const [result, setResult] = useState<{ key: string; detail: QuestionDetail | null; error: string | null } | null>(null);
  const key = `${id}#${isMock}#${tick}`;

  useEffect(() => {
    if (id === null) return;
    let cancelled = false;
    const fetchDetail = async (): Promise<QuestionDetail> => {
      if (isMock) {
        const found = store.find(q => q.id === id);
        if (!found) throw new ApiError(404, 'not found');
        return found;
      }
      const res = await apiFetch(`/api/v1/questions/${id}`, { credentials: 'include' });
      if (!res.ok) throw new ApiError(res.status, `API lỗi ${res.status}`);
      return await res.json() as QuestionDetail;
    };
    fetchDetail().then(
      detail => { if (!cancelled) setResult({ key, detail, error: null }); },
      e => { if (!cancelled) setResult({ key, detail: null, error: e instanceof ApiError && e.status === 404 ? 'Không tìm thấy câu hỏi' : describe(e, 'tải câu hỏi') }); },
    );
    return () => { cancelled = true; };
  }, [id, isMock, key, store, apiFetch]);

  const settled = result?.key === key ? result : null;
  return { detail: settled?.detail ?? null, error: settled?.error ?? null, loading: id !== null && !settled, reload: () => setTick(t => t + 1) };
}

/* ============================================================
   MODALS
   ============================================================ */

function QuestionModal({ id, catalog, isMock, mockStore, onClose, onSaved, onShowHistory }: {
  id: string | null; catalog: CatalogSubject[]; isMock: boolean; mockStore: QuestionDetail[];
  onClose: () => void; onSaved: (mockDetail?: QuestionDetail) => void; onShowHistory: (id: string) => void;
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
          ) : <QuestionForm key={detail?.id ?? 'new'} initial={isNew ? null : detail} catalog={catalog} isMock={isMock} onClose={onClose} onSaved={onSaved} onReload={reload} onShowHistory={onShowHistory} />}
      </div>
    </div>
  );
}

function QuestionForm({ initial, catalog, isMock, onClose, onSaved, onReload, onShowHistory }: {
  initial: QuestionDetail | null; catalog: CatalogSubject[]; isMock: boolean;
  onClose: () => void; onSaved: (mockDetail?: QuestionDetail) => void; onReload: () => void; onShowHistory: (id: string) => void;
}) {
  const { csrfToken, apiFetch } = useAuth();
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
          snapshots: [{
            revision, status: 'DRAFT', origin: 'MANUAL', stem: stem.trim(), options: options.map(o => o.trim()), correctOption: LABELS[correct],
            topic: { id: topic.id, name: topic.name }, difficulty, source: source.trim() || null, importBatchId: null, importRowNumber: null,
            createdByUserId: 'mock-user', createdAt: now, isLatest: true, isApprovedInUse: false,
          }, ...(initial?.snapshots ?? [])],
        };
        onSaved(saved);
        return;
      }

      const res = await apiFetch(isNew ? '/api/v1/questions' : `/api/v1/questions/${initial.id}`, {
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
>>>>>>> origin/dev
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
        {/* The history opens above this form, which stays mounted, so what the author has typed is kept. */}
        {!isNew && <button type="button" className="btn btn-secondary" style={{ marginRight: 'auto' }} onClick={() => onShowHistory(initial.id)} id="btn-editor-history"><History size={14} />Lịch sử revision</button>}
        <button className="btn btn-secondary" onClick={onClose} id="btn-cancel-question">Hủy</button>
        <button className={clsx('btn btn-primary', saving && 'btn-loading')} disabled={saving} onClick={save} id="btn-save-draft">Lưu nháp</button>
      </div>
    </>
  );
}
