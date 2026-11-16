'use client';

import React, { useEffect, useMemo, useState, useCallback } from 'react';
import {
  BookOpen, Plus, Search, RefreshCw, Pencil,
  Trash2, Check, Send, BookMarked, Lock, ChevronLeft, ChevronRight, X
} from 'lucide-react';
import clsx from 'clsx';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
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

const STATUS: Record<QuestionStatus, [string, string]> = {
  DRAFT: ['Bản nháp', 'badge-gray'],
  REVIEW_REQUIRED: ['Chờ duyệt', 'badge-amber'],
  APPROVED: ['Đã duyệt', 'badge-green'],
  REJECTED: ['Trả về cần sửa', 'badge-red'],
};

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
  };

  return (
    <div>
      <div className="page-header">
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
    </div>
  );
}

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
        </div>
      </div>
    </div>
  );
}
