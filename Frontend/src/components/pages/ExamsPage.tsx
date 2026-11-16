'use client';

import React, { useEffect, useState, useCallback } from 'react';
import {
  Plus, RefreshCw, Eye, Lock, CheckCircle2,
  AlertTriangle, ArrowLeft, Printer, Download, Send, Check, X, History, ShieldAlert, KeyRound
} from 'lucide-react';
import clsx from 'clsx';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
import { formatDateTime } from '@/lib/fetchOrMock';
import {
  AssembledExam,
  ExamAssemblyStatus,
  ExamCandidateStatus,
  ExamCandidateSubmission,
  useWorkflowStore,
  assembleExamFromBlueprint,
  finalizeExamInStore,
  submitExamToEventInStore,
  decideExamEventSelectionInStore,
} from '@/lib/workflowStore';

const STATUS_BADGE: Record<ExamAssemblyStatus, [string, string]> = {
  ASSEMBLING: ['Đang ráp (Thiếu câu)', 'badge-red'],
  VALIDATED: ['Đủ ma trận · Chờ chốt', 'badge-amber'],
  FINALIZED: ['Bản chốt bất biến', 'badge-green'],
};

const CANDIDATE_BADGE: Record<ExamCandidateStatus, [string, string]> = {
  DRAFT: ['Bản nháp', 'badge-gray'],
  PENDING_REVIEW: ['Chờ xét chọn', 'badge-amber'],
  APPROVED: ['Đề được chọn chính thức', 'badge-green'],
  NOT_SELECTED: ['Không được chọn', 'badge-gray'],
  REJECTED: ['Trả về yêu cầu sửa', 'badge-red'],
  SUPERSEDED: ['Đã thay bằng bản mới', 'badge-gray'],
};

export default function ExamsPage({ onNavigate }: { onNavigate?: (href: string) => void }) {
  const { user, primaryRole } = useAuth();
  const { success, error: showError } = useToast();
  const wf = useWorkflowStore();

  const [mainTab, setMainTab] = useState<'assembly' | 'selection'>('assembly');
  const [apiExams, setApiExams] = useState<AssembledExam[] | null>(null);
  const [loading, setLoading] = useState(true);
  // Open first exam in A4 preview by default or allow switching
  const [previewId, setPreviewId] = useState<string | null>(() => wf.exams[0]?.id ?? null);
  const [filter, setFilter] = useState<'' | ExamAssemblyStatus>('');
  const [showAssembleModal, setShowAssembleModal] = useState(false);

  // ExamEvent selection state
  const [selectedSubId, setSelectedSubId] = useState<string | null>(null);
  const [rejectingSub, setRejectingSub] = useState(false);
  const [rejectReason, setRejectReason] = useState('');

  const load = useCallback(async () => {
    try {
      const res = await fetch('/api/v1/exams', { credentials: 'include' });
      if (res.ok) {
        const data = (await res.json()) as AssembledExam[];
        if (Array.isArray(data)) {
          setApiExams(data);
          return;
        }
      }
      setApiExams(null);
    } catch {
      setApiExams(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let active = true;
    const run = async () => {
      await load();
      if (!active) return;
    };
    void run();
    return () => {
      active = false;
    };
  }, [load]);

  const exams = apiExams ?? wf.exams;
  const previewExam = exams.find(e => e.id === previewId) ?? null;

  const handleFinalize = (exam: AssembledExam) => {
    const actor = user?.userName ?? 'tbm.toan.thpt.a';
    const res = finalizeExamInStore(exam.id, actor);
    if (!res.ok) {
      showError(res.message);
      return;
    }
    success(res.message);
  };

  const handleSubmitToEvent = (exam: AssembledExam) => {
    const event = wf.examEvents[0];
    if (!event) return;
    const actor = user?.userName ?? exam.author;
    const res = submitExamToEventInStore({
      exam,
      eventId: event.id,
      teacherUserName: actor,
    });
    if (!res.ok) {
      showError(res.message);
      return;
    }
    success(res.message);
    setPreviewId(null);
    setMainTab('selection');
  };

  const shown = exams.filter(e => !filter || e.status === filter);
  const currentEvent = wf.examEvents[0];
  const eventSubmissions = wf.examSubmissions.filter(
    s => !currentEvent || s.eventId === currentEvent.id,
  );
  const currentlySelectedSub = eventSubmissions.find(
    s => s.id === currentEvent?.selectedSubmissionId || s.status === 'APPROVED',
  );
  const selectedSubmission: ExamCandidateSubmission | null =
    eventSubmissions.find(s => s.id === selectedSubId) ?? eventSubmissions[0] ?? null;

  const canSelectExam = primaryRole === 'DepartmentHead';
  const isSelfSubmission =
    Boolean(user?.userName) && selectedSubmission?.teacherUserName === user?.userName;

  const handleDecideEventSubmission = (decision: 'SELECT' | 'REJECT') => {
    if (!selectedSubmission) return;
    const reviewer = user?.userName ?? 'tbm.toan.thpt.a';
    const res = decideExamEventSelectionInStore({
      submissionId: selectedSubmission.id,
      expectedVersion: selectedSubmission.expectedVersion,
      reviewerUserName: reviewer,
      decision,
      reason: decision === 'REJECT' ? rejectReason : undefined,
    });
    if (!res.ok) {
      showError(res.message);
      return;
    }
    success(res.message);
    setRejectingSub(false);
    setRejectReason('');
  };

  return (
    <div>
      <div className="page-header no-print">
        <div>
          <h1 className="page-title">Xem trước đề thi khổ A4, Xét chọn &amp; Bản chốt</h1>
          <p className="page-subtitle">
            Kiểm tra tờ đề in A4, đối chiếu bảng đáp án theo quyền và xét chọn phiên bản đề chính thức cho kỳ kiểm tra
          </p>
        </div>
        <div className="page-actions">
          <button
            className="btn btn-secondary"
            onClick={() => {
              setLoading(true);
              void load();
            }}
            id="btn-refresh-exams"
          >
            <RefreshCw size={14} />
          </button>
          <button
            className="btn btn-primary"
            id="btn-create-exam"
            onClick={() => setShowAssembleModal(true)}
          >
            <Plus size={14} />
            Ráp đề mới từ ma trận
          </button>
        </div>
      </div>

      {/* Main Navigation Tabs */}
      <div className="tabs no-print">
        <div
          className={clsx('tab-item', mainTab === 'assembly' && 'active')}
          onClick={() => setMainTab('assembly')}
          id="tab-exam-assembly"
        >
          1. Bản in đề thi A4 &amp; Danh mục đề đã ráp
          <span className="tab-count">{exams.length}</span>
        </div>
        <div
          className={clsx('tab-item', mainTab === 'selection' && 'active')}
          onClick={() => setMainTab('selection')}
          id="tab-exam-selection"
        >
          2. Xét chọn đề theo đợt kiểm tra
          <span className="tab-count">
            {eventSubmissions.filter(s => s.status === 'PENDING_REVIEW').length}
          </span>
        </div>
      </div>

      {mainTab === 'assembly' ? (
        <>
          {/* Compact selector strip of assembled exams */}
          <div
            className="no-print"
            style={{
              display: 'flex',
              gap: 8,
              flexWrap: 'wrap',
              alignItems: 'center',
              marginBottom: 16,
              padding: '10px 14px',
              background: '#EFECE1',
              border: '1px solid var(--rule-base)',
              borderRadius: 4,
            }}
          >
            <span style={{ fontSize: 12.5, fontWeight: 700, color: 'var(--ink-700)' }}>
              Chọn bản đề trên bàn:
            </span>
            {shown.map(e => (
              <button
                key={e.id}
                type="button"
                onClick={() => setPreviewId(e.id)}
                id={`btn-preview-${e.id}`}
                className={clsx(
                  'btn btn-sm',
                  previewExam?.id === e.id ? 'btn-primary' : 'btn-secondary',
                )}
              >
                {e.status === 'FINALIZED' ? <Lock size={12} /> : <Eye size={12} />}
                <span>{e.title}</span>
                <span className="mono-num" style={{ opacity: 0.85 }}>
                  ({e.questions.length} câu)
                </span>
              </button>
            ))}

            <div style={{ marginLeft: 'auto', display: 'flex', gap: 6 }}>
              {(
                [
                  ['', 'Tất cả'],
                  ['FINALIZED', 'Đã chốt'],
                  ['VALIDATED', 'Chờ chốt'],
                ] as [typeof filter, string][]
              ).map(([k, l]) => (
                <button
                  key={k || 'all'}
                  className={clsx('btn btn-sm', filter === k ? 'btn-secondary' : 'btn-ghost')}
                  onClick={() => setFilter(k)}
                  id={`tab-exam-${k || 'all'}`}
                >
                  {l}
                </button>
              ))}
            </div>
          </div>

          {loading ? (
            <div className="skeleton" style={{ height: 420 }} />
          ) : previewExam ? (
            <ExamA4PreviewDesk
              exam={previewExam}
              currentUserName={user?.userName ?? 'tbm.toan.thpt.a'}
              primaryRole={primaryRole}
              onClosePreview={() => setPreviewId(null)}
              onFinalize={() => handleFinalize(previewExam)}
              onSubmitToEvent={() => handleSubmitToEvent(previewExam)}
              onGoBlueprints={() => onNavigate?.('/blueprints')}
            />
          ) : (
            <div style={{ display: 'grid', gap: 10 }}>
              {shown.map(e => {
                const pts = Math.round(e.questions.reduce((a, q) => a + q.points, 0) * 100) / 100;
                return (
                  <div
                    key={e.id}
                    className="card"
                    style={{
                      padding: '14px 18px',
                      display: 'flex',
                      gap: 14,
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      flexWrap: 'wrap',
                    }}
                  >
                    <div>
                      <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 4 }}>
                        <strong style={{ fontFamily: 'var(--font-serif)', fontSize: 16 }}>
                          {e.title}
                        </strong>
                        <span className={clsx('badge', STATUS_BADGE[e.status][1])}>
                          {STATUS_BADGE[e.status][0]}
                        </span>
                      </div>
                      <div style={{ fontSize: 12.5, color: 'var(--ink-600)' }}>
                        Ma trận: {e.blueprintName} ·{' '}
                        <span className="mono-num">
                          {e.questions.length} câu · {pts}/{e.totalPoints}đ · {e.durationMinutes} phút
                        </span>{' '}
                        · Người soạn: <code>{e.author}</code>
                      </div>
                    </div>
                    <button
                      className="btn btn-primary btn-sm"
                      onClick={() => setPreviewId(e.id)}
                    >
                      <Eye size={13} /> Mở tờ đề A4
                    </button>
                  </div>
                );
              })}
            </div>
          )}
        </>
      ) : (
        /* Tab 2: ExamEvent Selection Desk with Explicit Selected Version Pointer & Marginalia */
        <div>
          {currentEvent && (
            <div
              className="card"
              style={{
                padding: '14px 18px',
                marginBottom: 16,
                background: '#EFECE1',
                border: '1px solid var(--rule-strong)',
              }}
            >
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  gap: 12,
                  flexWrap: 'wrap',
                }}
              >
                <div>
                  <div style={{ fontSize: 11, fontWeight: 700, textTransform: 'uppercase', color: 'var(--ink-500)', letterSpacing: '0.05em' }}>
                    Đợt kiểm tra cấp trường · {currentEvent.schoolName} ({currentEvent.departmentName})
                  </div>
                  <div style={{ fontFamily: 'var(--font-serif)', fontSize: 17, fontWeight: 700, color: 'var(--ink-900)' }}>
                    {currentEvent.name}
                  </div>
                </div>
                <div>
                  {currentlySelectedSub ? (
                    <div
                      style={{
                        padding: '6px 12px',
                        background: 'var(--brand-50)',
                        border: '1px solid var(--brand-600)',
                        borderRadius: 3,
                        fontSize: 12.5,
                        color: 'var(--brand-700)',
                      }}
                    >
                      <CheckCircle2 size={13} style={{ display: 'inline', verticalAlign: 'middle', marginRight: 5 }} />
                      <strong>Phiên bản đang được chọn:</strong>{' '}
                      <code>
                        {currentlySelectedSub.id} · rev {currentlySelectedSub.revision}
                      </code>{' '}
                      ({currentlySelectedSub.teacherUserName})
                    </div>
                  ) : (
                    <span className="badge badge-amber" style={{ fontSize: 12, padding: '4px 10px' }}>
                      Chưa có phiên bản đề nào được chọn chính thức
                    </span>
                  )}
                </div>
              </div>
            </div>
          )}

          <div
            style={{
              display: 'grid',
              gridTemplateColumns: '320px 1fr',
              gap: 18,
              alignItems: 'start',
            }}
            className="selection-grid"
          >
            {/* Left: Submitted Exam Versions */}
            <div className="table-container">
              <div className="table-toolbar">
                <strong style={{ fontFamily: 'var(--font-serif)', fontSize: 14.5 }}>
                  Các phiên bản đề đã nộp ({eventSubmissions.length})
                </strong>
              </div>
              <div>
                {eventSubmissions.map(sub => {
                  const isChosen =
                    currentEvent?.selectedSubmissionId === sub.id || sub.status === 'APPROVED';
                  return (
                    <button
                      key={sub.id}
                      type="button"
                      onClick={() => {
                        setSelectedSubId(sub.id);
                        setRejectingSub(false);
                      }}
                      id={`sub-item-${sub.id}`}
                      style={{
                        display: 'block',
                        width: '100%',
                        textAlign: 'left',
                        padding: '12px 14px',
                        border: 'none',
                        borderBottom: '1px solid var(--rule-light)',
                        background:
                          selectedSubmission?.id === sub.id ? '#EBF3F1' : 'var(--paper-sheet)',
                        borderLeft: `3px solid ${
                          isChosen
                            ? 'var(--brand-600)'
                            : selectedSubmission?.id === sub.id
                            ? 'var(--ink-900)'
                            : 'transparent'
                        }`,
                        cursor: 'pointer',
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                        <code style={{ fontSize: 11.5, fontWeight: 700 }}>
                          {sub.id} · Phiên bản rev {sub.revision}
                        </code>
                        <span className={clsx('badge', CANDIDATE_BADGE[sub.status][1])}>
                          {CANDIDATE_BADGE[sub.status][0]}
                        </span>
                      </div>
                      <div style={{ fontWeight: 600, fontSize: 13.5, color: 'var(--ink-900)', marginBottom: 4 }}>
                        {sub.examTitle}
                      </div>
                      <div style={{ fontSize: 11.5, color: 'var(--ink-500)' }}>
                        Giáo viên: <code>{sub.teacherUserName}</code> ·{' '}
                        <span className="mono-num">
                          {sub.questionCount} câu ({sub.totalPoints}đ)
                        </span>
                      </div>
                      {isChosen && (
                        <div style={{ marginTop: 6 }}>
                          <span className="badge badge-green" style={{ fontSize: 11 }}>
                            Đang được chọn cho kỳ kiểm tra
                          </span>
                        </div>
                      )}
                      {sub.rejectReason && (
                        <div
                          style={{
                            marginTop: 6,
                            fontSize: 11.5,
                            color: 'var(--danger-500)',
                            fontWeight: 600,
                          }}
                        >
                          Bút phê trả về: {sub.rejectReason}
                        </div>
                      )}
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Right: Selected Submission Detail + Event Marginalia History */}
            <div style={{ display: 'grid', gap: 16 }}>
              <div className="card" style={{ padding: 20 }}>
                {!selectedSubmission ? (
                  <div className="table-empty">
                    <div className="table-empty-text">Chọn một phiên bản đề bên trái để đối chiếu</div>
                  </div>
                ) : (
                  <>
                    <div
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'flex-start',
                        gap: 10,
                        flexWrap: 'wrap',
                        paddingBottom: 12,
                        marginBottom: 14,
                        borderBottom: '2px solid var(--ink-900)',
                      }}
                    >
                      <div>
                        <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--ink-500)', textTransform: 'uppercase' }}>
                          Chi tiết phiên bản đề dự tuyển
                        </div>
                        <h2 style={{ fontFamily: 'var(--font-serif)', fontSize: 18, fontWeight: 700 }}>
                          {selectedSubmission.examTitle}
                        </h2>
                        <div style={{ fontSize: 12.5, color: 'var(--ink-600)', marginTop: 2 }}>
                          Mã hồ sơ: <code>{selectedSubmission.id}</code> ·{' '}
                          <strong>Phiên bản (Revision) {selectedSubmission.revision}</strong> ·{' '}
                          <span className="mono-num">
                            expectedVersion={selectedSubmission.expectedVersion}
                          </span>{' '}
                          · Người gửi: <code>{selectedSubmission.teacherUserName}</code>
                        </div>
                      </div>
                      <span className={clsx('badge', CANDIDATE_BADGE[selectedSubmission.status][1])}>
                        {CANDIDATE_BADGE[selectedSubmission.status][0]}
                      </span>
                    </div>

                    {selectedSubmission.rejectReason && (
                      <div className="margin-note red-ink" style={{ marginBottom: 14 }}>
                        <strong>Lý do trả về của Phiên bản {selectedSubmission.revision}:</strong>{' '}
                        {selectedSubmission.rejectReason}
                      </div>
                    )}

                    <div
                      style={{
                        border: '1px solid var(--rule-base)',
                        borderRadius: 3,
                        overflow: 'hidden',
                        marginBottom: 16,
                      }}
                    >
                      {selectedSubmission.questions.map((q, idx) => (
                        <div
                          key={q.id}
                          style={{
                            padding: '10px 14px',
                            borderBottom:
                              idx < selectedSubmission.questions.length - 1
                                ? '1px solid var(--rule-light)'
                                : 'none',
                            fontSize: 13.5,
                          }}
                        >
                          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 3 }}>
                            <strong style={{ fontFamily: 'var(--font-serif)' }}>
                              Câu {idx + 1}. {q.content}
                            </strong>
                            <code style={{ fontSize: 11.5, color: 'var(--ink-500)' }}>
                              {q.id}@rev{q.revision} ({q.points}đ)
                            </code>
                          </div>
                          <div style={{ fontSize: 12.5, color: 'var(--ink-600)' }}>
                            {q.options
                              .map((o, i) => `${String.fromCharCode(65 + i)}. ${o}`)
                              .join(' · ')}
                          </div>
                        </div>
                      ))}
                    </div>

                    {canSelectExam ? (
                      <div style={{ borderTop: '1px solid var(--rule-base)', paddingTop: 14 }}>
                        {isSelfSubmission && (
                          <div className="margin-note red-ink" style={{ marginBottom: 12 }}>
                            <ShieldAlert size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: 5 }} />
                            Người soạn đề không được tự xét chọn chính phiên bản đề của mình.
                          </div>
                        )}

                        {rejectingSub && (
                          <div className="form-group">
                            <label className="form-label" htmlFor="exam-reject-reason" style={{ color: 'var(--danger-500)' }}>
                              Lý do trả lại Phiên bản {selectedSubmission.revision} <span className="required">*</span>
                            </label>
                            <textarea
                              id="exam-reject-reason"
                              className="form-input"
                              rows={2}
                              value={rejectReason}
                              onChange={e => setRejectReason(e.target.value)}
                              placeholder="Ghi rõ lý do trả lại đề để giáo viên điều chỉnh và nộp phiên bản mới…"
                            />
                          </div>
                        )}

                        <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                          {rejectingSub ? (
                            <>
                              <button
                                className="btn btn-secondary btn-sm"
                                onClick={() => {
                                  setRejectingSub(false);
                                  setRejectReason('');
                                }}
                              >
                                Hủy
                              </button>
                              <button
                                className="btn btn-danger btn-sm"
                                disabled={isSelfSubmission}
                                onClick={() => handleDecideEventSubmission('REJECT')}
                                id="btn-confirm-reject-exam-sub"
                              >
                                <X size={13} />
                                Xác nhận trả lại
                              </button>
                            </>
                          ) : (
                            <>
                              <button
                                className="btn btn-secondary btn-sm"
                                style={{ color: 'var(--danger-500)' }}
                                disabled={isSelfSubmission}
                                onClick={() => setRejectingSub(true)}
                                id="btn-return-exam-sub"
                              >
                                <X size={13} />
                                Trả lại kèm lý do
                              </button>
                              <button
                                className="btn btn-primary btn-sm"
                                disabled={
                                  isSelfSubmission || selectedSubmission.status === 'APPROVED'
                                }
                                onClick={() => handleDecideEventSubmission('SELECT')}
                                id="btn-select-exam-sub"
                              >
                                <Check size={13} />
                                Chọn phiên bản này làm đề chính thức
                              </button>
                            </>
                          )}
                        </div>
                      </div>
                    ) : (
                      <div className="margin-note">
                        Quyền chọn hoặc trả lại đề cấp trường thuộc về <strong>Trưởng Bộ Môn</strong> phụ trách đúng trường và tổ bộ môn.
                      </div>
                    )}
                  </>
                )}
              </div>

              {currentEvent && (
                <div className="card" style={{ padding: 16, background: '#F4F0E5' }}>
                  <div
                    style={{
                      fontSize: 11.5,
                      fontWeight: 700,
                      color: 'var(--ink-600)',
                      textTransform: 'uppercase',
                      letterSpacing: '0.05em',
                      marginBottom: 8,
                      display: 'flex',
                      alignItems: 'center',
                      gap: 6,
                    }}
                  >
                    <History size={13} />
                    Sổ lịch sử xét chọn đề của đợt kiểm tra
                  </div>
                  <div style={{ display: 'grid', gap: 6 }}>
                    {currentEvent.history.map(h => (
                      <div
                        key={h.id}
                        style={{
                          fontSize: 12.5,
                          padding: '8px 10px',
                          background: 'var(--paper-sheet)',
                          border: '1px solid var(--rule-base)',
                          borderRadius: 3,
                        }}
                      >
                        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
                          <strong>
                            [{h.action}] {h.submissionId} · Phiên bản rev {h.revision} (bởi{' '}
                            <code>{h.actor}</code>)
                          </strong>
                          <span className="mono-num" style={{ color: 'var(--ink-500)', fontSize: 11.5 }}>
                            {formatDateTime(h.occurredAt)}
                          </span>
                        </div>
                        <div style={{ color: 'var(--ink-700)' }}>{h.detail}</div>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {showAssembleModal && (
        <AssembleFromBlueprintModal
          onClose={() => setShowAssembleModal(false)}
          onAssembled={examId => {
            setShowAssembleModal(false);
            setPreviewId(examId);
          }}
        />
      )}

      <style>{`
        @media (max-width: 960px) {
          .selection-grid { grid-template-columns: 1fr !important; }
        }
      `}</style>
    </div>
  );
}

function AssembleFromBlueprintModal({
  onClose,
  onAssembled,
}: {
  onClose: () => void;
  onAssembled: (examId: string) => void;
}) {
  const { user, primaryRole } = useAuth();
  const { success } = useToast();
  const wf = useWorkflowStore();
  const [bpId, setBpId] = useState(wf.blueprints[0]?.id ?? '');
  const [scopeLevel, setScopeLevel] = useState<'SCHOOL' | 'MINISTRY'>(
    primaryRole === 'MinistryAdmin' ? 'MINISTRY' : 'SCHOOL',
  );

  const handleCreate = () => {
    const bp = wf.blueprints.find(b => b.id === bpId);
    if (!bp) return;
    const actor = user?.userName ?? 'gv.nguyen.van.a';
    const { exam, ok } = assembleExamFromBlueprint(bp, actor, scopeLevel);
    success(
      ok
        ? `Đã ráp đề "${exam.title}" đủ ma trận`
        : `Đã ráp đề "${exam.title}" (Còn thiếu câu hỏi đã duyệt theo ma trận)`,
    );
    onAssembled(exam.id);
  };

  return (
    <div className="modal-overlay" onClick={e => e.target === e.currentTarget && onClose()}>
      <div className="modal">
        <div className="modal-header">
          <div>
            <div className="modal-title">Ráp đề thi từ Ma trận đặc tả</div>
            <div className="modal-subtitle">
              Chỉ lấy các câu hỏi đã duyệt và ghim cứng mã phiên bản
            </div>
          </div>
          <button className="modal-close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal-body">
          <div className="form-group">
            <label className="form-label" htmlFor="select-bp-assemble">
              Chọn ma trận đề
            </label>
            <select
              id="select-bp-assemble"
              className="form-select"
              value={bpId}
              onChange={e => setBpId(e.target.value)}
            >
              {wf.blueprints.map(b => (
                <option key={b.id} value={b.id}>
                  {b.name} ({b.totalPoints} điểm · {b.durationMinutes} phút)
                </option>
              ))}
            </select>
          </div>
          <div className="form-group">
            <label className="form-label" htmlFor="select-scope-assemble">
              Cấp tổ chức kỳ thi
            </label>
            <select
              id="select-scope-assemble"
              className="form-select"
              value={scopeLevel}
              onChange={e => setScopeLevel(e.target.value as 'SCHOOL' | 'MINISTRY')}
            >
              <option value="SCHOOL">Cấp trường (Giáo viên soạn → Trưởng bộ môn xét chọn/chốt)</option>
              <option value="MINISTRY">
                Cấp Bộ (Yêu cầu 2 tài khoản Quản trị Bộ độc lập soạn và xác nhận)
              </option>
            </select>
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn btn-secondary" onClick={onClose}>
            Hủy
          </button>
          <button className="btn btn-primary" onClick={handleCreate} id="btn-confirm-assemble">
            Ráp đề &amp; Mở tờ đề A4
          </button>
        </div>
      </div>
    </div>
  );
}

function ExamA4PreviewDesk({
  exam,
  currentUserName,
  primaryRole,
  onClosePreview,
  onFinalize,
  onSubmitToEvent,
  onGoBlueprints,
}: {
  exam: AssembledExam;
  currentUserName: string;
  primaryRole: string | null;
  onClosePreview: () => void;
  onFinalize: () => void;
  onSubmitToEvent: () => void;
  onGoBlueprints: () => void;
}) {
  const [showAnswerKey, setShowAnswerKey] = useState(true);

  const sumPts =
    Math.round(exam.questions.reduce((acc, q) => acc + q.points, 0) * 100) / 100;
  const uniqueIds = new Set(exam.questions.map(q => q.id)).size === exam.questions.length;
  const allApproved = exam.questions.every(q => q.status === 'APPROVED');
  const pointsMatch = sumPts === exam.totalPoints;
  const ministryRuleOk =
    exam.scopeLevel !== 'MINISTRY' || exam.author !== currentUserName;

  const canFinalize =
    exam.status !== 'FINALIZED' &&
    exam.issues.length === 0 &&
    allApproved &&
    uniqueIds &&
    pointsMatch &&
    (primaryRole === 'DepartmentHead' || primaryRole === 'MinistryAdmin');

  const canSeeAnswers =
    primaryRole === 'Teacher' ||
    primaryRole === 'DepartmentHead' ||
    primaryRole === 'MinistryAdmin';

  const exportSnapshotJson = () => {
    const payload = {
      examId: exam.id,
      title: exam.title,
      blueprintId: exam.blueprintId,
      status: exam.status,
      durationMinutes: exam.durationMinutes,
      totalPoints: exam.totalPoints,
      author: exam.author,
      confirmedBy: exam.confirmedBy ?? null,
      finalizedAt: exam.finalizedAt ?? null,
      pinnedRevisions: exam.questions.map((q, idx) => ({
        order: idx + 1,
        questionId: q.id,
        revision: q.revision,
        version: q.version,
        points: q.points,
      })),
    };
    const blob = new Blob([JSON.stringify(payload, null, 2)], {
      type: 'application/json;charset=utf-8',
    });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `${exam.id}-snapshot.json`;
    a.click();
    URL.revokeObjectURL(a.href);
  };

  return (
    <div>
      {/* External Desk Control Bar (Outside the A4 Paper Sheet) */}
      <div
        className="card no-print"
        style={{
          padding: '12px 16px',
          marginBottom: 18,
          background: '#F2EFE4',
          border: '1px solid var(--rule-strong)',
        }}
      >
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            gap: 12,
            flexWrap: 'wrap',
            marginBottom: 10,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
            <button
              className="btn btn-ghost btn-sm"
              onClick={onClosePreview}
              id="btn-back-exams"
            >
              <ArrowLeft size={13} />
              Thu gọn bản in
            </button>
            <span className={clsx('badge', STATUS_BADGE[exam.status][1])}>
              {STATUS_BADGE[exam.status][0]}
            </span>
            <span style={{ fontSize: 12.5, color: 'var(--ink-700)' }}>
              Kiểm tra trước chốt:{' '}
              <strong className="mono-num">
                {exam.questions.length} câu · {sumPts.toFixed(2)}/{exam.totalPoints.toFixed(2)}đ
              </strong>{' '}
              · {uniqueIds ? 'Không trùng câu' : 'Có câu trùng'}
            </span>
          </div>

          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {canSeeAnswers && (
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                onClick={() => setShowAnswerKey(v => !v)}
                id="btn-toggle-answer-key"
              >
                <KeyRound size={13} />
                {showAnswerKey ? 'Ẩn đáp án trên đề' : 'Hiện đáp án & mã phiên bản'}
              </button>
            )}
            <button
              className="btn btn-secondary btn-sm"
              onClick={exportSnapshotJson}
              id="btn-export-snapshot"
            >
              <Download size={13} />
              Xuất JSON
            </button>
            <button
              className="btn btn-secondary btn-sm"
              onClick={() => window.print()}
              id="btn-print-exam"
            >
              <Printer size={13} />
              In tờ đề A4
            </button>
            {exam.status !== 'FINALIZED' &&
              (primaryRole === 'Teacher' || primaryRole === 'DepartmentHead') && (
                <button
                  className="btn btn-secondary btn-sm"
                  disabled={exam.issues.length > 0}
                  onClick={onSubmitToEvent}
                  id="btn-submit-exam-event"
                >
                  <Send size={13} />
                  Gửi vào đợt kiểm tra
                </button>
              )}
            {exam.status !== 'FINALIZED' && (
              <button
                className="btn btn-primary btn-sm"
                disabled={!canFinalize}
                onClick={onFinalize}
                id="btn-finalize-exam"
              >
                <Lock size={13} />
                Chốt đề bất biến
              </button>
            )}
          </div>
        </div>

        {/* Pre-finalization verification strip */}
        {exam.issues.length > 0 ? (
          <div className="margin-note red-ink" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 10 }}>
            <span>
              <AlertTriangle size={13} style={{ display: 'inline', verticalAlign: 'middle', marginRight: 5 }} />
              <strong>Chưa đủ điều kiện chốt đề:</strong> {exam.issues.join(' · ')}
            </span>
            <button className="btn btn-secondary btn-sm" onClick={onGoBlueprints}>
              Về bàn ma trận bổ sung
            </button>
          </div>
        ) : (
          <div style={{ fontSize: 12, color: 'var(--brand-700)', display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <span>✓ Tất cả câu hỏi đều đã được duyệt (APPROVED)</span>
            <span>✓ Khớp tổng điểm ma trận ({exam.totalPoints.toFixed(2)} điểm)</span>
            <span>✓ Không trùng lặp định danh câu hỏi</span>
            {exam.scopeLevel === 'MINISTRY' && (
              <span>
                {ministryRuleOk
                  ? '✓ Đạt quy tắc 2 người độc lập (Đề cấp Bộ)'
                  : '⚠ Cần cán bộ cấp Bộ khác người soạn để xác nhận'}
              </span>
            )}
          </div>
        )}
      </div>

      {/* True A4 Printed Exam Sheet */}
      <article className="a4-paper-sheet" aria-label="Bản xem trước tờ đề thi khổ A4">
        {/* Classical Vietnamese Exam Paper Header */}
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: '1fr 1.25fr',
            gap: 16,
            paddingBottom: 14,
            marginBottom: 16,
            borderBottom: '3px double var(--ink-900)',
          }}
        >
          <div style={{ textAlign: 'center', lineHeight: 1.4 }}>
            <div style={{ fontSize: 12.5, fontWeight: 600, textTransform: 'uppercase' }}>
              SỞ GIÁO DỤC VÀ ĐÀO TẠO
            </div>
            <div style={{ fontSize: 13.5, fontWeight: 700, textTransform: 'uppercase' }}>
              TRƯỜNG THPT NGUYỄN DU
            </div>
            <div
              style={{
                width: 90,
                height: 1,
                background: 'var(--ink-900)',
                margin: '4px auto 6px',
              }}
            />
            <div style={{ fontSize: 12, fontStyle: 'italic' }}>
              {exam.status === 'FINALIZED' ? 'ĐỀ CHÍNH THỨC (BẢN CHỐT)' : 'BẢN DỰ THẢO TRƯỚC KHI CHỐT'}
            </div>
            <div style={{ fontSize: 11.5, color: 'var(--ink-600)' }}>
              (Đề thi gồm có 01 trang)
            </div>
          </div>

          <div style={{ textAlign: 'center', lineHeight: 1.45 }}>
            <div style={{ fontSize: 14.5, fontWeight: 700, textTransform: 'uppercase' }}>
              {exam.title}
            </div>
            <div style={{ fontSize: 13.5, fontWeight: 700, marginTop: 2 }}>
              Môn thi: {exam.subject.toUpperCase()}
            </div>
            <div style={{ fontSize: 12.5 }}>
              Thời gian làm bài: <span className="mono-num">{exam.durationMinutes}</span> phút{' '}
              <em>(không kể thời gian phát đề)</em>
            </div>
            <div
              className="mono-num"
              style={{
                display: 'inline-block',
                marginTop: 6,
                padding: '2px 12px',
                border: '1.5px solid var(--ink-900)',
                fontWeight: 700,
                fontSize: 12.5,
              }}
            >
              Mã đề: 101 · Thang điểm: {exam.totalPoints.toFixed(2)}
            </div>
          </div>
        </div>

        {/* Candidate Information Box */}
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: '2fr 1fr',
            gap: 12,
            padding: '8px 12px',
            border: '1px solid var(--ink-800)',
            marginBottom: 20,
            fontSize: 13.5,
          }}
        >
          <div>Họ và tên thí sinh: ....................................................................................</div>
          <div>Số báo danh: ....................................</div>
        </div>

        {/* Exam Questions List */}
        <div style={{ display: 'grid', gap: 18 }}>
          {exam.questions.map((q, i) => (
            <div
              key={q.id}
              style={{
                fontSize: 15,
                lineHeight: 1.6,
                paddingBottom: 12,
                borderBottom: '1px dotted var(--rule-base)',
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 10, marginBottom: 6 }}>
                <div>
                  <strong>Câu {i + 1}</strong>{' '}
                  <span className="mono-num" style={{ fontSize: 13, color: 'var(--ink-600)' }}>
                    ({q.points.toFixed(2)} điểm)
                  </span>
                  <strong>:</strong> {q.content}
                </div>
                {canSeeAnswers && showAnswerKey && (
                  <code
                    style={{
                      fontSize: 11,
                      color: 'var(--brand-700)',
                      background: 'var(--brand-50)',
                      border: '1px solid var(--brand-200)',
                      padding: '1px 6px',
                      borderRadius: 2,
                      flexShrink: 0,
                    }}
                  >
                    {q.id}@rev{q.revision} · v{q.version}
                  </code>
                )}
              </div>

              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: '1fr 1fr',
                  gap: '6px 20px',
                  paddingLeft: 12,
                }}
              >
                {q.options.map((o, j) => {
                  const isCorrect = canSeeAnswers && showAnswerKey && j === q.correctIndex;
                  return (
                    <div
                      key={j}
                      style={{
                        fontWeight: isCorrect ? 700 : 400,
                        color: isCorrect ? 'var(--brand-700)' : 'var(--ink-900)',
                        textDecoration: isCorrect ? 'underline' : 'none',
                      }}
                    >
                      <strong>{String.fromCharCode(65 + j)}.</strong> {o}
                      {isCorrect && ' (Đáp án)'}
                    </div>
                  );
                })}
              </div>
            </div>
          ))}
        </div>

        {/* End of Exam Marker */}
        <div style={{ textAlign: 'center', marginTop: 28, fontSize: 13.5 }}>
          <div style={{ fontWeight: 700, letterSpacing: '0.08em' }}>— HẾT —</div>
          <div style={{ fontStyle: 'italic', fontSize: 12.5, color: 'var(--ink-600)', marginTop: 4 }}>
            Thí sinh không được sử dụng tài liệu. Cán bộ coi thi không giải thích gì thêm.
          </div>
        </div>

        {/* Answer Key Table on Sheet (when toggled by authorized role) */}
        {canSeeAnswers && showAnswerKey && (
          <div
            style={{
              marginTop: 28,
              paddingTop: 14,
              borderTop: '2px solid var(--ink-900)',
              fontFamily: 'var(--font-sans)',
            }}
          >
            <div style={{ fontSize: 12, fontWeight: 700, textTransform: 'uppercase', marginBottom: 8 }}>
              Bảng đáp án &amp; Dấu niêm phong phiên bản câu hỏi (Chỉ hiển thị cho Giáo viên / Hội đồng duyệt)
            </div>
            <table>
              <thead>
                <tr>
                  <th>Thứ tự câu</th>
                  <th>Đáp án đúng</th>
                  <th>Thang điểm</th>
                  <th>Mã câu hỏi &amp; Phiên bản ghim cứng</th>
                  <th>Chủ đề / Độ khó</th>
                </tr>
              </thead>
              <tbody>
                {exam.questions.map((q, idx) => (
                  <tr key={q.id}>
                    <td className="mono-num" style={{ fontWeight: 700 }}>
                      Câu {idx + 1}
                    </td>
                    <td>
                      <span className="badge badge-green mono-num">
                        Phương án {String.fromCharCode(65 + q.correctIndex)}
                      </span>
                    </td>
                    <td className="mono-num">{q.points.toFixed(2)}đ</td>
                    <td>
                      <code>
                        {q.id} · rev {q.revision} (v{q.version})
                      </code>
                    </td>
                    <td>
                      {q.subject} / {q.topic} ({q.difficulty})
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </article>

      <style>{`
        @media print {
          .no-print, .sidebar, .topbar { display: none !important; }
          .main-content { margin-left: 0 !important; }
          .a4-paper-sheet { box-shadow: none !important; border: none !important; padding: 0 !important; }
        }
      `}</style>
    </div>
  );
}
