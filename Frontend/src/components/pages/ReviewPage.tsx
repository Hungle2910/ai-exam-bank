'use client';

import React, { useEffect, useState, useCallback } from 'react';
import {
  ClipboardCheck, Check, X, RefreshCw, Search, Clock,
  Database, BookMarked, ShieldAlert, FileText
} from 'lucide-react';
import clsx from 'clsx';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
import { formatDateTime } from '@/lib/fetchOrMock';
import {
  useWorkflowStore,
  decideQuestionReviewInStore,
  QuestionCitation,
  QuestionRevisionRecord,
  ReviewDecisionRecord,
} from '@/lib/workflowStore';

type Difficulty = 'EASY' | 'MEDIUM' | 'HARD';
type SourceKind = 'MANUAL' | 'IMPORT' | 'AI_DRAFT' | 'SEED';

interface ReviewItem {
  id: string;
  revision: number;
  version: number;
  content: string;
  options: string[];
  correctIndex: number;
  subject: string;
  topic: string;
  difficulty: Difficulty;
  author: string;
  ownerUserName?: string;
  submittedAt: string;
  status: 'REVIEW_REQUIRED';
  source: SourceKind;
  explanation?: string;
  citation?: QuestionCitation;
  revisions?: QuestionRevisionRecord[];
  decisions?: ReviewDecisionRecord[];
}

function isReviewItem(value: unknown): value is ReviewItem {
  if (typeof value !== 'object' || value === null) return false;
  const item = value as Record<string, unknown>;
  return (
    typeof item.id === 'string' &&
    item.id.length > 0 &&
    Number.isInteger(item.revision) &&
    Number(item.revision) > 0 &&
    Number.isInteger(item.version) &&
    Number(item.version) > 0 &&
    typeof item.content === 'string' &&
    Array.isArray(item.options) &&
    item.options.length === 4 &&
    item.options.every(option => typeof option === 'string') &&
    Number.isInteger(item.correctIndex) &&
    Number(item.correctIndex) >= 0 &&
    Number(item.correctIndex) < 4 &&
    typeof item.subject === 'string' &&
    typeof item.topic === 'string' &&
    (item.difficulty === 'EASY' || item.difficulty === 'MEDIUM' || item.difficulty === 'HARD') &&
    typeof item.author === 'string' &&
    typeof item.submittedAt === 'string' &&
    item.status === 'REVIEW_REQUIRED' &&
    (item.source === 'MANUAL' ||
      item.source === 'IMPORT' ||
      item.source === 'AI_DRAFT' ||
      item.source === 'SEED')
  );
}

const DIFF_LABEL: Record<Difficulty, [string, string]> = {
  EASY: ['Dễ', 'badge-green'],
  MEDIUM: ['Trung bình', 'badge-amber'],
  HARD: ['Khó', 'badge-red'],
};

const SOURCE_LABEL: Record<SourceKind, [string, string]> = {
  MANUAL: ['Soạn tay', 'badge-gray'],
  IMPORT: ['Bảng CSV', 'badge-teal'],
  AI_DRAFT: ['Gợi ý AI', 'badge-amber'],
  SEED: ['Khởi tạo', 'badge-gray'],
};

export default function ReviewPage() {
  const auth = useAuth();
  const csrfToken = auth?.csrfToken ?? null;
  const currentUserName = auth?.user?.userName ?? null;
  const { success, error: showError } = useToast();
  const wf = useWorkflowStore();

  const [apiItems, setApiItems] = useState<ReviewItem[]>([]);
  const [loadError, setLoadError] = useState(false);
  const [loading, setLoading] = useState(true);
  const [sandboxMode, setSandboxMode] = useState(false);

  const [search, setSearch] = useState('');
  const [diffFilter, setDiffFilter] = useState<'' | Difficulty>('');
  const [sourceFilter, setSourceFilter] = useState<'' | SourceKind>('');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [rejecting, setRejecting] = useState(false);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [lastApprovedNote, setLastApprovedNote] = useState<string | null>(null);

  const fetchPending = useCallback(async () => {
    try {
      const response = await fetch('/api/v1/reviews/pending', { credentials: 'include' });
      if (!response.ok) throw new Error(`Review API returned ${response.status}`);
      const data: unknown = await response.json();
      if (!Array.isArray(data) || !data.every(isReviewItem)) {
        throw new Error('Invalid review response');
      }
      setApiItems(data);
      setLoadError(false);
      setSandboxMode(false);
    } catch {
      setApiItems([]);
      setLoadError(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let active = true;
    const run = async () => {
      await fetchPending();
      if (!active) return;
    };
    void run();
    return () => {
      active = false;
    };
  }, [fetchPending]);

  const sandboxItems: ReviewItem[] = wf.questions
    .filter(q => q.status === 'REVIEW_REQUIRED')
    .map(q => ({
      id: q.id,
      revision: q.revision,
      version: q.version,
      content: q.content,
      options: q.options,
      correctIndex: q.correctIndex,
      subject: q.subject,
      topic: q.topic,
      difficulty: q.difficulty,
      author: q.author,
      ownerUserName: q.ownerUserName,
      submittedAt: q.submittedAt ?? q.updatedAt,
      status: 'REVIEW_REQUIRED',
      source: q.source,
      explanation: q.explanation,
      citation: q.citation,
      revisions: q.revisions,
      decisions: q.decisions,
    }));

  const items = sandboxMode ? sandboxItems : apiItems;

  const visible = items.filter(
    i =>
      (i.content.toLowerCase().includes(search.toLowerCase()) ||
        i.subject.toLowerCase().includes(search.toLowerCase()) ||
        i.topic.toLowerCase().includes(search.toLowerCase())) &&
      (!diffFilter || i.difficulty === diffFilter) &&
      (!sourceFilter || i.source === sourceFilter),
  );
  const selected = items.find(i => i.id === selectedId) ?? visible[0] ?? null;

  const isSelfReview =
    Boolean(currentUserName) &&
    Boolean(selected) &&
    (selected?.author === currentUserName || selected?.ownerUserName === currentUserName);

  const decide = async (approve: boolean) => {
    if (!selected || busy) return;
    if (!approve && !reason.trim()) {
      showError('Vui lòng nhập lý do trả lại');
      return;
    }

    if (sandboxMode) {
      const reviewer = currentUserName ?? 'tbm.toan.thpt.a';
      const targetLabel = `${selected.id} (Phiên bản rev ${selected.revision} · v${selected.version})`;
      const res = decideQuestionReviewInStore({
        questionId: selected.id,
        revision: selected.revision,
        expectedVersion: selected.version,
        reviewerUserName: reviewer,
        approve,
        reason: approve ? undefined : reason.trim(),
      });
      if (!res.ok) {
        showError(res.message);
        return;
      }
      setLastApprovedNote(
        approve
          ? `Vừa phê duyệt câu hỏi ${targetLabel} vào ngân hàng đề.`
          : `Vừa trả lại câu hỏi ${targetLabel} kèm bút phê chỉnh sửa.`,
      );
      success(res.message);
      setRejecting(false);
      setReason('');
      setSelectedId(null);
      return;
    }

    if (!csrfToken) {
      showError('Phiên bảo mật chưa sẵn sàng. Vui lòng tải lại trang.');
      return;
    }
    setBusy(true);
    try {
      const res = await fetch(
        `/api/v1/reviews/${encodeURIComponent(selected.id)}/revisions/${selected.revision}/decision`,
        {
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken },
          body: JSON.stringify({
            decision: approve ? 'Approve' : 'Reject',
            expectedVersion: selected.version,
            reason: approve ? null : reason.trim(),
          }),
        },
      );
      if (res.status === 409) {
        showError('Câu hỏi đã được người khác xử lý (HTTP 409). Đang tải lại…');
        setLoading(true);
        await fetchPending();
        return;
      }
      if (res.status === 403) {
        showError('Bạn không có quyền duyệt câu hỏi này hoặc vi phạm quy tắc tự duyệt (HTTP 403)');
        return;
      }
      if (!res.ok) {
        showError('Không thể lưu quyết định');
        return;
      }
      setLoading(true);
      await fetchPending();
      success(approve ? 'Đã duyệt câu hỏi' : 'Đã trả lại câu hỏi');
      setRejecting(false);
      setReason('');
      setSelectedId(null);
    } catch {
      showError('Lỗi kết nối');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-title">Bàn duyệt câu hỏi &amp; Lề ghi chú thẩm định</h1>
          <p className="page-subtitle">
            Đối chiếu nội dung câu hỏi, nguồn sách giáo khoa và bút phê lịch sử theo từng phiên bản trước khi đưa vào ngân hàng đề
          </p>
        </div>
        <div className="page-actions">
          {sandboxMode && (
            <span className="badge badge-amber">
              <Database size={12} />
              Hồ sơ mẫu trên bàn duyệt
            </span>
          )}
          <button
            className="btn btn-secondary"
            onClick={() => {
              setLoading(true);
              void fetchPending();
            }}
            id="btn-refresh-review"
          >
            <RefreshCw size={14} />
            Làm mới
          </button>
        </div>
      </div>

      {lastApprovedNote && (
        <div className="margin-note green-ink" style={{ marginBottom: 14 }}>
          <strong>Xác nhận thao tác thẩm định:</strong> {lastApprovedNote}
        </div>
      )}

      <div className="tabs">
        <div className="tab-item active">
          Hồ sơ chờ thẩm định
          <span className="tab-count">{items.length}</span>
        </div>
      </div>

      <div
        style={{
          display: 'grid',
          gridTemplateColumns: '290px 1fr',
          gap: 18,
          alignItems: 'start',
        }}
        className="review-desk-grid"
      >
        {/* Left: Queue Index */}
        <div className="table-container">
          <div className="table-toolbar" style={{ flexWrap: 'wrap', gap: 6 }}>
            <div className="search-bar" style={{ width: '100%' }}>
              <Search size={13} className="search-bar-icon" />
              <input
                placeholder="Tìm mã câu, chủ đề, nội dung…"
                value={search}
                onChange={e => setSearch(e.target.value)}
                id="review-search"
              />
            </div>
            <select
              className="form-select"
              style={{ flex: 1, fontSize: 12 }}
              value={diffFilter}
              onChange={e => setDiffFilter(e.target.value as Difficulty | '')}
              aria-label="Lọc độ khó"
              id="review-diff-filter"
            >
              <option value="">Mọi mức độ</option>
              <option value="EASY">Dễ</option>
              <option value="MEDIUM">Trung bình</option>
              <option value="HARD">Khó</option>
            </select>
            <select
              className="form-select"
              style={{ flex: 1, fontSize: 12 }}
              value={sourceFilter}
              onChange={e => setSourceFilter(e.target.value as SourceKind | '')}
              aria-label="Lọc nguồn"
              id="review-source-filter"
            >
              <option value="">Mọi nguồn</option>
              <option value="MANUAL">Soạn tay</option>
              <option value="IMPORT">Bảng CSV</option>
              <option value="AI_DRAFT">Gợi ý AI</option>
            </select>
          </div>

          {loading ? (
            <div style={{ padding: 14 }}>
              {[0, 1, 2].map(i => (
                <div
                  key={i}
                  className="skeleton"
                  style={{ height: 58, marginBottom: 8 }}
                />
              ))}
            </div>
          ) : loadError && !sandboxMode ? (
            <div className="table-empty" style={{ padding: 20 }}>
              <div className="table-empty-text">
                Không tải được hàng chờ duyệt. Vui lòng thử lại.
              </div>
              <div className="table-empty-sub" style={{ marginTop: 4 }}>
                Dịch vụ <code>/api/v1/reviews/pending</code> hiện chưa phản hồi (Chế độ khóa an toàn).
              </div>
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                style={{ marginTop: 12 }}
                onClick={() => setSandboxMode(true)}
                id="btn-enable-review-sandbox"
              >
                <FileText size={13} />
                Mở hồ sơ chờ duyệt trên bàn ({sandboxItems.length} câu)
              </button>
            </div>
          ) : visible.length === 0 ? (
            <div className="table-empty">
              <div className="table-empty-icon">
                <ClipboardCheck />
              </div>
              <div className="table-empty-text">Không có câu hỏi nào</div>
              <div className="table-empty-sub">Đã rà soát hết các bản thảo chờ duyệt</div>
            </div>
          ) : (
            <div>
              {visible.map(i => {
                const isCurrent = selected?.id === i.id;
                return (
                  <button
                    key={i.id}
                    onClick={() => {
                      setSelectedId(i.id);
                      setRejecting(false);
                    }}
                    id={`review-item-${i.id}`}
                    className="review-list-item"
                    style={{
                      display: 'block',
                      width: '100%',
                      textAlign: 'left',
                      padding: '11px 13px',
                      border: 'none',
                      cursor: 'pointer',
                      borderBottom: '1px solid var(--rule-light)',
                      background: isCurrent ? '#EBF3F1' : 'var(--paper-sheet)',
                      borderLeft: `3px solid ${
                        isCurrent ? 'var(--brand-600)' : 'transparent'
                      }`,
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 3 }}>
                      <code style={{ fontSize: 11.5, fontWeight: 700, color: 'var(--brand-700)' }}>
                        {i.id} · Phiên bản {i.revision}
                      </code>
                      <span className={clsx('badge', SOURCE_LABEL[i.source][1])}>
                        {SOURCE_LABEL[i.source][0]}
                      </span>
                    </div>
                    <div
                      style={{
                        fontSize: 13,
                        fontWeight: 600,
                        color: 'var(--ink-900)',
                        marginBottom: 5,
                        display: '-webkit-box',
                        WebkitLineClamp: 2,
                        WebkitBoxOrient: 'vertical',
                        overflow: 'hidden',
                      }}
                    >
                      {i.content}
                    </div>
                    <div style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: 11.5, color: 'var(--ink-500)' }}>
                      <span>{i.subject} / {i.topic}</span>
                      <span>·</span>
                      <span>{DIFF_LABEL[i.difficulty][0]}</span>
                    </div>
                  </button>
                );
              })}
            </div>
          )}
        </div>

        {/* Right: Manuscript Sheet + Marginalia Notes Column */}
        {!selected ? (
          <div className="card" style={{ padding: 36 }}>
            <div className="table-empty">
              <div className="table-empty-text">Chọn một bản thảo câu hỏi bên trái để mở tờ thẩm định</div>
            </div>
          </div>
        ) : (
          <div
            className="manuscript-with-margin"
            style={{
              display: 'grid',
              gridTemplateColumns: '1.35fr 310px',
              background: 'var(--paper-sheet)',
              border: '1px solid var(--rule-strong)',
              borderRadius: 4,
            }}
          >
            {/* Manuscript Main Sheet */}
            <div style={{ padding: '22px 24px', borderRight: '1px solid var(--rule-base)' }}>
              {/* Explicit Version Identification Header */}
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  flexWrap: 'wrap',
                  gap: 8,
                  paddingBottom: 12,
                  marginBottom: 16,
                  borderBottom: '2px solid var(--ink-900)',
                }}
              >
                <div>
                  <div style={{ fontSize: 11, fontWeight: 700, textTransform: 'uppercase', color: 'var(--ink-500)', letterSpacing: '0.05em' }}>
                    Phiếu thẩm định nội dung câu hỏi
                  </div>
                  <div style={{ fontFamily: 'var(--font-serif)', fontSize: 16, fontWeight: 700, color: 'var(--ink-900)' }}>
                    Câu hỏi <code style={{ fontSize: 15 }}>{selected.id}</code> · Đang xét{' '}
                    <span style={{ color: 'var(--brand-700)', textDecoration: 'underline' }}>
                      Phiên bản (Revision) {selected.revision}
                    </span>
                  </div>
                </div>
                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                  <span className="badge badge-gray mono-num">
                    expectedVersion={selected.version}
                  </span>
                  <span className={clsx('badge', SOURCE_LABEL[selected.source][1])}>
                    {SOURCE_LABEL[selected.source][0]}
                  </span>
                  <span className={clsx('badge', DIFF_LABEL[selected.difficulty][1])}>
                    {DIFF_LABEL[selected.difficulty][0]}
                  </span>
                </div>
              </div>

              <div style={{ fontSize: 12.5, color: 'var(--ink-600)', marginBottom: 12 }}>
                Môn học: <strong>{selected.subject}</strong> · Chủ đề: <strong>{selected.topic}</strong> · Người gửi:{' '}
                <code style={{ fontWeight: 600 }}>{selected.author}</code> · Gửi lúc {formatDateTime(selected.submittedAt)}
              </div>

              {/* Question Stem in Spectral Serif */}
              <h2
                style={{
                  fontFamily: 'var(--font-serif)',
                  fontSize: 18,
                  fontWeight: 600,
                  color: 'var(--ink-900)',
                  lineHeight: 1.55,
                  marginBottom: 16,
                  padding: '12px 16px',
                  background: '#F7F4EB',
                  border: '1px solid var(--rule-base)',
                  borderRadius: 3,
                }}
              >
                {selected.content}
              </h2>

              {/* 4 Options */}
              <div style={{ display: 'grid', gap: 8, marginBottom: 16 }}>
                {selected.options.map((o, idx) => {
                  const correct = idx === selected.correctIndex;
                  return (
                    <div
                      key={idx}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: 10,
                        padding: '9px 12px',
                        borderRadius: 3,
                        border: `1px solid ${
                          correct ? 'var(--brand-600)' : 'var(--rule-base)'
                        }`,
                        background: correct ? 'var(--brand-50)' : '#FFFFFF',
                      }}
                    >
                      <span
                        className="mono-num"
                        style={{
                          width: 24,
                          height: 24,
                          borderRadius: 3,
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          fontWeight: 700,
                          fontSize: 12,
                          background: correct ? 'var(--brand-600)' : '#EAE5D9',
                          color: correct ? '#FFFFFF' : 'var(--ink-800)',
                        }}
                      >
                        {String.fromCharCode(65 + idx)}
                      </span>
                      <span style={{ fontSize: 14, color: 'var(--ink-900)', fontWeight: correct ? 600 : 400 }}>
                        {o}
                      </span>
                      {correct && (
                        <span
                          className="badge badge-green"
                          style={{ marginLeft: 'auto', fontSize: 11 }}
                        >
                          <Check size={11} /> Đáp án đúng
                        </span>
                      )}
                    </div>
                  );
                })}
              </div>

              {selected.explanation && (
                <div className="margin-note" style={{ marginBottom: 14 }}>
                  <strong>Lời giải / Căn cứ đáp án:</strong> {selected.explanation}
                </div>
              )}

              {selected.citation && (
                <div
                  style={{
                    background: '#F2EFE4',
                    border: '1px solid var(--rule-strong)',
                    padding: '10px 12px',
                    borderRadius: 3,
                    fontSize: 12.5,
                    color: 'var(--ink-800)',
                    marginBottom: 16,
                    display: 'flex',
                    gap: 8,
                  }}
                >
                  <BookMarked size={15} style={{ flexShrink: 0, marginTop: 2, color: 'var(--brand-600)' }} />
                  <div>
                    <strong>Nguồn đối chiếu SGK (<code>{selected.citation.sourceId}</code>):</strong>{' '}
                    {selected.citation.source} ({selected.citation.locator}) —{' '}
                    <em>“{selected.citation.excerpt}”</em>
                  </div>
                </div>
              )}

              {isSelfReview && (
                <div className="margin-note red-ink" style={{ marginBottom: 16 }}>
                  <div style={{ display: 'flex', gap: 6, alignItems: 'center', fontWeight: 700 }}>
                    <ShieldAlert size={14} />
                    <span>Cấm tự duyệt (Self-review ban):</span>
                  </div>
                  <div style={{ marginTop: 2 }}>
                    Tài khoản đang mở (<code>{currentUserName}</code>) trùng với người biên soạn câu hỏi này. Cần Trưởng bộ môn độc lập thẩm định.
                  </div>
                </div>
              )}

              {/* Decision Action Box */}
              <div
                style={{
                  borderTop: '1px solid var(--rule-base)',
                  paddingTop: 16,
                  marginTop: 8,
                }}
              >
                {rejecting && (
                  <div className="form-group">
                    <label className="form-label" htmlFor="reject-reason" style={{ color: 'var(--danger-500)' }}>
                      Bút phê lý do trả lại cho Phiên bản {selected.revision} <span className="required">*</span>
                    </label>
                    <textarea
                      id="reject-reason"
                      className="form-input"
                      rows={3}
                      value={reason}
                      onChange={e => setReason(e.target.value)}
                      placeholder="Ghi rõ điểm cần sửa ở câu dẫn hoặc phương án nhiễu để giáo viên tạo phiên bản mới…"
                      autoFocus
                      style={{ resize: 'vertical', borderColor: 'var(--danger-500)' }}
                    />
                  </div>
                )}

                <div style={{ display: 'flex', gap: 10, justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 12, color: 'var(--ink-500)' }}>
                    Quyết định sẽ áp dụng cho <strong>{selected.id} · Phiên bản {selected.revision}</strong>
                  </span>
                  <div style={{ display: 'flex', gap: 8 }}>
                    {rejecting ? (
                      <>
                        <button
                          className="btn btn-secondary"
                          onClick={() => {
                            setRejecting(false);
                            setReason('');
                          }}
                          id="btn-cancel-reject"
                        >
                          Hủy
                        </button>
                        <button
                          className={clsx('btn btn-danger', busy && 'btn-loading')}
                          disabled={busy}
                          onClick={() => void decide(false)}
                          id="btn-confirm-reject"
                        >
                          <X size={14} />
                          Xác nhận trả lại
                        </button>
                      </>
                    ) : (
                      <>
                        <button
                          className="btn btn-secondary"
                          style={{ color: 'var(--danger-500)', borderColor: 'var(--danger-200)' }}
                          onClick={() => setRejecting(true)}
                          id="btn-reject"
                        >
                          <X size={14} />
                          Trả lại
                        </button>
                        <button
                          className={clsx('btn btn-success', busy && 'btn-loading')}
                          disabled={busy}
                          onClick={() => void decide(true)}
                          id="btn-approve"
                        >
                          <Check size={14} />
                          Duyệt
                        </button>
                      </>
                    )}
                  </div>
                </div>
              </div>
            </div>

            {/* Right Marginalia Column: Revisions & Attached Decision Reasons */}
            <aside
              style={{
                padding: '18px 16px',
                background: '#F4F0E5',
                display: 'flex',
                flexDirection: 'column',
                gap: 14,
              }}
              aria-label="Lề ghi chú thẩm định và lịch sử phiên bản"
            >
              <div style={{ borderBottom: '1px solid var(--rule-strong)', paddingBottom: 8 }}>
                <div style={{ fontSize: 11, fontWeight: 700, textTransform: 'uppercase', color: 'var(--ink-500)', letterSpacing: '0.06em' }}>
                  Lề ghi chú thẩm định
                </div>
                <div style={{ fontFamily: 'var(--font-serif)', fontSize: 14.5, fontWeight: 700, color: 'var(--ink-900)' }}>
                  Lịch sử phiên bản &amp; Bút phê
                </div>
              </div>

              {/* Pair each revision with its decision/rejection reason right beside it */}
              {(selected.revisions && selected.revisions.length > 0
                ? selected.revisions
                : [
                    {
                      revision: selected.revision,
                      version: selected.version,
                      content: selected.content,
                      options: selected.options,
                      correctIndex: selected.correctIndex,
                      status: 'REVIEW_REQUIRED' as const,
                      author: selected.author,
                      updatedAt: selected.submittedAt,
                    },
                  ]
              ).map(rev => {
                const isCurrentViewing = rev.revision === selected.revision;
                const matchingDecisions = (selected.decisions ?? []).filter(
                  d => d.revision === rev.revision,
                );
                const rejectDec = matchingDecisions.find(d => d.action === 'REJECT');

                return (
                  <div
                    key={rev.revision}
                    style={{
                      padding: '10px 12px',
                      background: isCurrentViewing ? '#FFFFFF' : '#FAF7F0',
                      border: `1px solid ${
                        isCurrentViewing ? 'var(--brand-600)' : 'var(--rule-base)'
                      }`,
                      borderRadius: 3,
                      fontSize: 12.5,
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 4 }}>
                      <strong className="mono-num" style={{ color: 'var(--ink-900)' }}>
                        Phiên bản {rev.revision} (v{rev.version})
                      </strong>
                      {isCurrentViewing ? (
                        <span className="badge badge-green" style={{ fontSize: 10.5 }}>
                          Đang mở xem
                        </span>
                      ) : (
                        <span className="badge badge-gray" style={{ fontSize: 10.5 }}>
                          {rev.status === 'REJECTED' ? 'Đã trả về' : rev.status}
                        </span>
                      )}
                    </div>

                    <div style={{ fontSize: 11.5, color: 'var(--ink-500)', marginBottom: 6 }}>
                      Người gửi: <code>{rev.author}</code> ·{' '}
                      <Clock size={10} style={{ display: 'inline', verticalAlign: 'middle' }} />{' '}
                      {formatDateTime(rev.updatedAt)}
                    </div>

                    {rev.note && (
                      <div style={{ fontSize: 12, color: 'var(--ink-700)', marginBottom: 6 }}>
                        Ghi chú soạn thảo: <em>“{rev.note}”</em>
                      </div>
                    )}

                    {/* Attached Rejection Reason right next to the related revision */}
                    {rejectDec && (
                      <div className="margin-note red-ink" style={{ marginTop: 6, padding: '6px 9px' }}>
                        <div style={{ fontWeight: 700, fontSize: 11.5 }}>
                          Bút phê trả về ({rejectDec.actor}):
                        </div>
                        <div style={{ marginTop: 2 }}>{rejectDec.reason}</div>
                      </div>
                    )}

                    {matchingDecisions
                      .filter(d => d.action !== 'REJECT')
                      .map(d => (
                        <div
                          key={d.id}
                          style={{
                            fontSize: 11.5,
                            color: 'var(--ink-600)',
                            marginTop: 4,
                            borderTop: '1px dashed var(--rule-base)',
                            paddingTop: 4,
                          }}
                        >
                          • {d.action === 'APPROVE' ? 'Phê duyệt' : 'Nộp duyệt'} bởi{' '}
                          <code>{d.actor}</code> ({formatDateTime(d.occurredAt)})
                        </div>
                      ))}
                  </div>
                );
              })}
            </aside>
          </div>
        )}
      </div>

      <style>{`
        .review-list-item:hover { background: #F4F0E5 !important; }
        @media (max-width: 1100px) {
          .manuscript-with-margin { grid-template-columns: 1fr !important; }
        }
        @media (max-width: 860px) {
          .review-desk-grid { grid-template-columns: 1fr !important; }
        }
      `}</style>
    </div>
  );
}
