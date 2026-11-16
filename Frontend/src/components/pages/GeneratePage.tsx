'use client';

import React, { useState, useEffect, useMemo, useCallback } from 'react';
import {
  Cpu, Check, X, Pencil, BookMarked, Loader2,
  RotateCcw, ArrowRight
} from 'lucide-react';
import clsx from 'clsx';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
import {
  Difficulty,
  QuestionCitation,
  useWorkflowStore,
  computeSlotStats,
  upsertBankQuestion,
  appendLocalAudit,
} from '@/lib/workflowStore';
import {
  AI_SAFETY_POLICY_VERSION,
  JobApiItem,
  enqueueBackgroundJob,
  fetchJobStatus,
  isTerminalJobStatus,
  retryBackgroundJob,
  cancelBackgroundJob,
  validateAiDraftSafety,
} from '@/lib/jobClient';

const DIFF: Record<Difficulty, [string, string]> = {
  EASY: ['Dễ', 'badge-green'],
  MEDIUM: ['Trung bình', 'badge-amber'],
  HARD: ['Khó', 'badge-red'],
};

interface MissingSlotCandidate {
  id: string;
  blueprintId: string;
  blueprintName: string;
  subject: string;
  topic: string;
  difficulty: Difficulty;
  pointsPerQuestion: number;
  requiredCount: number;
  approvedCount: number;
  missingCount: number;
}

interface GeneratedSuggestion {
  id: string;
  slotId: string;
  subject: string;
  topic: string;
  difficulty: Difficulty;
  points: number;
  content: string;
  options: string[];
  correctIndex: number;
  explanation: string;
  citation: QuestionCitation;
  decision?: 'ACCEPTED' | 'DISCARDED';
}

const RAG_TEMPLATES: Array<{
  subject: string;
  topic: string;
  difficulty: Difficulty;
  content: string;
  options: string[];
  correctIndex: number;
  explanation: string;
  citation: QuestionCitation;
}> = [
  {
    subject: 'Toán',
    topic: 'Đạo hàm',
    difficulty: 'MEDIUM',
    content: 'Hàm số y = x⁴ − 2x² + 1 có bao nhiêu điểm cực trị trên tập số thực ℝ?',
    options: ['3 điểm cực trị', '1 điểm cực trị', '2 điểm cực trị', '0 điểm cực trị'],
    correctIndex: 0,
    explanation: 'y′ = 4x³ − 4x = 4x(x² − 1) = 0 có 3 nghiệm phân biệt x = 0, x = ±1 và y′ đổi dấu qua cả 3 điểm.',
    citation: {
      sourceId: 'kb-math12-ch1',
      source: 'SGK Giải tích 12 — Chương I: Ứng dụng đạo hàm',
      locator: 'Bài 2: Cực trị của hàm số, tr. 13',
      excerpt: 'Nếu f′(x) đổi dấu khi x qua x₀ thì hàm số đạt cực trị tại điểm x₀.',
    },
  },
  {
    subject: 'Toán',
    topic: 'Đạo hàm',
    difficulty: 'MEDIUM',
    content: 'Giá trị lớn nhất của hàm số y = −x² + 4x − 1 trên đoạn [0; 3] bằng bao nhiêu?',
    options: ['3', '2', '−1', '4'],
    correctIndex: 0,
    explanation: 'y′ = −2x + 4 = 0 ⇔ x = 2 ∈ [0; 3]. Ta có y(0) = −1, y(2) = 3, y(3) = 2 ⇒ max = 3.',
    citation: {
      sourceId: 'kb-math12-ch1',
      source: 'SGK Giải tích 12 — Chương I: Ứng dụng đạo hàm',
      locator: 'Bài 3: Giá trị lớn nhất và giá trị nhỏ nhất của hàm số, tr. 20',
      excerpt: 'Muốn tìm GTLN, GTNN của hàm số liên tục trên đoạn [a; b], ta tính giá trị tại các điểm tới hạn và hai đầu mút.',
    },
  },
  {
    subject: 'Toán',
    topic: 'Logarit',
    difficulty: 'HARD',
    content: 'Tổng tất cả các nghiệm thực của phương trình log₂²x − 3log₂x + 2 = 0 bằng bao nhiêu?',
    options: ['6', '3', '4', '2'],
    correctIndex: 0,
    explanation: 'Đặt t = log₂x ⇒ t² − 3t + 2 = 0 ⇔ t = 1 hoặc t = 2 ⇒ x = 2¹ = 2 hoặc x = 2² = 4. Tổng hai nghiệm là 2 + 4 = 6.',
    citation: {
      sourceId: 'kb-math12-ch2',
      source: 'SGK Giải tích 12 — Chương II: Hàm số lũy thừa, mũ và logarit',
      locator: 'Bài 5: Phương trình mũ và phương trình logarit, tr. 82',
      excerpt: 'Phương pháp đặt ẩn phụ t = logₐx (x > 0): giải phương trình đại số theo t rồi suy ra x = a^t.',
    },
  },
  {
    subject: 'Toán',
    topic: 'Đạo hàm',
    difficulty: 'EASY',
    content: 'Đạo hàm của hàm số y = x⁴ − 4x tại điểm x = 1 bằng?',
    options: ['0', '4', '−3', '1'],
    correctIndex: 0,
    explanation: 'y′(x) = 4x³ − 4 ⇒ y′(1) = 4(1)³ − 4 = 0.',
    citation: {
      sourceId: 'kb-math12-ch1',
      source: 'SGK Giải tích 12 — Chương I: Ứng dụng đạo hàm',
      locator: 'Mục 1.1, tr. 6',
      excerpt: 'Công thức đạo hàm hàm lũy thừa (x^n)′ = n·x^(n−1).',
    },
  },
];

const ACTIVE_GEN_JOB_KEY = 'ai-exam-bank:active-gen-job-id';

export default function GeneratePage({ onNavigate }: { onNavigate?: (href: string) => void }) {
  const { user } = useAuth();
  const { success, error: showError } = useToast();
  const wf = useWorkflowStore();

  const missingSlots = useMemo<MissingSlotCandidate[]>(() => {
    const out: MissingSlotCandidate[] = [];
    wf.blueprints.forEach(bp => {
      bp.slots.forEach(s => {
        const stats = computeSlotStats(s, wf.questions);
        if (stats.missingCount > 0) {
          out.push({
            id: s.id,
            blueprintId: bp.id,
            blueprintName: bp.name,
            subject: s.subject,
            topic: s.topic,
            difficulty: s.difficulty,
            pointsPerQuestion: s.pointsPerQuestion,
            requiredCount: s.questionCount,
            approvedCount: stats.approvedCount,
            missingCount: stats.missingCount,
          });
        }
      });
    });
    return out;
  }, [wf.blueprints, wf.questions]);

  const [selectedSlotIds, setSelectedSlotIds] = useState<string[]>(() => {
    if (wf.navIntent?.slotIds?.length) return wf.navIntent.slotIds;
    return missingSlots.map(s => s.id);
  });

  const [activeJob, setActiveJob] = useState<JobApiItem | null>(null);
  const [simulateErrorMode, setSimulateErrorMode] = useState(false);
  const [suggestions, setSuggestions] = useState<GeneratedSuggestion[]>(() => {
    // Provide initial draft suggestions on desk if navigated from a missing slot
    if (missingSlots.length === 0) return [];
    const firstSlot = missingSlots[0];
    return [
      {
        id: `sug-${firstSlot.id}-1`,
        slotId: firstSlot.id,
        subject: firstSlot.subject,
        topic: firstSlot.topic,
        difficulty: firstSlot.difficulty,
        points: firstSlot.pointsPerQuestion,
        content: RAG_TEMPLATES[0].content,
        options: [...RAG_TEMPLATES[0].options],
        correctIndex: RAG_TEMPLATES[0].correctIndex,
        explanation: RAG_TEMPLATES[0].explanation,
        citation: RAG_TEMPLATES[0].citation,
      },
    ];
  });
  const [editingId, setEditingId] = useState<string | null>(null);

  const materializeSuggestions = useCallback(
    (slotIds: string[]) => {
      const targetSlots =
        missingSlots.filter(s => slotIds.includes(s.id)).length > 0
          ? missingSlots.filter(s => slotIds.includes(s.id))
          : missingSlots;

      const built: GeneratedSuggestion[] = [];
      targetSlots.forEach(slot => {
        const matchingTemplates = RAG_TEMPLATES.filter(
          t => t.topic.toLowerCase() === slot.topic.toLowerCase() && t.difficulty === slot.difficulty,
        );
        const pool = matchingTemplates.length > 0 ? matchingTemplates : RAG_TEMPLATES;
        const count = Math.min(slot.missingCount, 2);
        for (let i = 0; i < count; i++) {
          const tpl = pool[(built.length + i) % pool.length];
          built.push({
            id: `sug-${slot.id}-${i + 1}`,
            slotId: slot.id,
            subject: slot.subject,
            topic: slot.topic,
            difficulty: slot.difficulty,
            points: slot.pointsPerQuestion,
            content: tpl.content,
            options: [...tpl.options],
            correctIndex: tpl.correctIndex,
            explanation: tpl.explanation,
            citation: tpl.citation,
          });
        }
      });
      setSuggestions(built);
    },
    [missingSlots],
  );

  useEffect(() => {
    if (typeof window === 'undefined') return;
    const savedJobId = window.localStorage.getItem(ACTIVE_GEN_JOB_KEY);
    if (!savedJobId) return;

    let active = true;
    fetchJobStatus(savedJobId).then(job => {
      if (!active || !job) return;
      setActiveJob(job);
      if (job.status === 'Completed') {
        materializeSuggestions(selectedSlotIds);
      }
    });
    return () => {
      active = false;
    };
  }, [materializeSuggestions, selectedSlotIds]);

  useEffect(() => {
    if (!activeJob || isTerminalJobStatus(activeJob.status)) return;

    const timer = setInterval(async () => {
      const updated = await fetchJobStatus(activeJob.jobId);
      if (!updated) return;
      setActiveJob(updated);
      if (updated.status === 'Completed') {
        materializeSuggestions(selectedSlotIds);
        if (typeof window !== 'undefined') {
          window.localStorage.removeItem(ACTIVE_GEN_JOB_KEY);
        }
      }
    }, 900);

    return () => clearInterval(timer);
  }, [activeJob, materializeSuggestions, selectedSlotIds]);

  const isRunning = activeJob !== null && !isTerminalJobStatus(activeJob.status);

  const startGeneration = async () => {
    if (isRunning || selectedSlotIds.length === 0) return;
    setSuggestions([]);
    const payload = simulateErrorMode
      ? `slots:${selectedSlotIds.join(',')}:simulate-bedrock-throttled`
      : `slots:${selectedSlotIds.join(',')}:rag-v1`;
    const idempotencyKey = `gen-${selectedSlotIds.join('-')}-${Date.now()}`;

    const { job } = await enqueueBackgroundJob({
      jobType: 'ExamGeneration',
      payloadReference: payload,
      idempotencyKey,
    });
    setActiveJob(job);
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(ACTIVE_GEN_JOB_KEY, job.jobId);
    }

    appendLocalAudit({
      actor: user?.userName ?? 'gv.nguyen.van.a',
      action: 'DRAFT_GENERATED',
      target: job.jobId.slice(0, 12),
      detail: `Yêu cầu AI gợi ý câu hỏi cho ${selectedSlotIds.length} ô thiếu`,
      schoolName: 'Trường THPT Nguyễn Du',
      correlationId: job.jobId,
    });
  };

  const handleRetryJob = async () => {
    if (!activeJob) return;
    const retried = await retryBackgroundJob(
      activeJob.jobId,
      'Người dùng bấm Thử lại trên bàn Rà soát gợi ý AI',
    );
    if (!retried) {
      showError('Tác vụ không đủ điều kiện thử lại');
      return;
    }
    setActiveJob(retried);
    if (retried.status === 'Completed') {
      materializeSuggestions(selectedSlotIds);
      success('Đã khôi phục tác vụ và tạo gợi ý AI thành công');
    }
  };

  const handleCancelJob = async () => {
    if (!activeJob) return;
    await cancelBackgroundJob(activeJob.jobId);
    const updated = await fetchJobStatus(activeJob.jobId);
    if (updated) setActiveJob(updated);
    if (typeof window !== 'undefined') {
      window.localStorage.removeItem(ACTIVE_GEN_JOB_KEY);
    }
  };

  const decideSuggestion = (id: string, decision: GeneratedSuggestion['decision']) => {
    setSuggestions(prev => prev.map(s => (s.id === id ? { ...s, decision } : s)));
  };

  const acceptedSuggestions = suggestions.filter(s => s.decision === 'ACCEPTED');
  const pendingSuggestions = suggestions.filter(s => !s.decision);

  const commitAccepted = (submitForReview: boolean) => {
    if (acceptedSuggestions.length === 0) return;
    const actor = user?.userName ?? 'gv.nguyen.van.a';

    for (const item of acceptedSuggestions) {
      const gate = validateAiDraftSafety({
        stem: item.content,
        options: item.options,
        correctIndex: item.correctIndex,
        explanation: item.explanation,
      });
      if (!gate.succeeded) {
        showError(`Bộ lọc an toàn chặn câu "${item.id}": ${gate.error}`);
        return;
      }
    }

    acceptedSuggestions.forEach(item => {
      upsertBankQuestion({
        content: item.content,
        options: item.options,
        correctIndex: item.correctIndex,
        explanation: item.explanation,
        subject: item.subject,
        topic: item.topic,
        difficulty: item.difficulty,
        points: item.points,
        source: 'AI_DRAFT',
        actorUserName: actor,
        submitForReview,
        citation: item.citation,
        note: `Bản nháp gợi ý AI (${AI_SAFETY_POLICY_VERSION}) cho ô ${item.topic} (${item.difficulty})`,
      });
    });

    setSuggestions(prev => prev.filter(s => s.decision !== 'ACCEPTED'));
    success(
      submitForReview
        ? `Đã lưu và gửi ${acceptedSuggestions.length} câu hỏi sang Bàn duyệt câu hỏi`
        : `Đã lưu ${acceptedSuggestions.length} câu hỏi vào Mục lục ở trạng thái Bản nháp`,
    );
  };

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-title">Bàn rà soát gợi ý câu hỏi từ AI</h1>
          <p className="page-subtitle">
            Mỗi gợi ý được đặt cạnh đoạn trích sách giáo khoa và ghi chú kiểm tra; giáo viên chỉnh sửa hoặc xác nhận trước khi đưa vào mục lục
          </p>
        </div>
        <div className="page-actions">
          <span className="badge badge-gray mono-num">
            Quy tắc: {AI_SAFETY_POLICY_VERSION}
          </span>
        </div>
      </div>

      <div
        style={{ display: 'grid', gridTemplateColumns: '320px 1fr', gap: 18, alignItems: 'start' }}
        className="gen-grid"
      >
        {/* Left: Missing Matrix Slots & Inline Progress */}
        <div className="card" style={{ padding: 16 }}>
          <div
            style={{
              fontSize: 11.5,
              fontWeight: 700,
              color: 'var(--ink-600)',
              textTransform: 'uppercase',
              letterSpacing: '.05em',
              marginBottom: 10,
              borderBottom: '1px solid var(--rule-base)',
              paddingBottom: 6,
            }}
          >
            Ô ma trận đang yêu cầu gợi ý ({missingSlots.length})
          </div>

          {missingSlots.length === 0 ? (
            <div className="margin-note green-ink" style={{ marginBottom: 12 }}>
              Các ô trong ma trận hiện tại đã đủ câu hỏi đạt chuẩn.
            </div>
          ) : (
            <div style={{ display: 'grid', gap: 8, marginBottom: 12 }}>
              {missingSlots.map(s => {
                const checked = selectedSlotIds.includes(s.id);
                const slotProcessing = isRunning && checked;
                return (
                  <label
                    key={s.id}
                    style={{
                      display: 'block',
                      padding: '10px 12px',
                      borderRadius: 3,
                      cursor: isRunning ? 'not-allowed' : 'pointer',
                      border: `1px solid ${
                        checked ? 'var(--brand-600)' : 'var(--rule-base)'
                      }`,
                      background: checked ? '#EBF3F1' : 'var(--paper-sheet)',
                      position: 'relative',
                    }}
                  >
                    <span className="matrix-missing-corner" />
                    <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
                      <input
                        type="checkbox"
                        checked={checked}
                        disabled={isRunning}
                        id={`slot-select-${s.id}`}
                        style={{ marginTop: 3 }}
                        onChange={e =>
                          setSelectedSlotIds(prev =>
                            e.target.checked
                              ? [...prev, s.id]
                              : prev.filter(x => x !== s.id),
                          )
                        }
                      />
                      <div style={{ flex: 1 }}>
                        <div style={{ fontWeight: 700, fontSize: 13.5, color: 'var(--ink-900)' }}>
                          {s.subject} · {s.topic}{' '}
                          <span className={clsx('badge', DIFF[s.difficulty][1])}>
                            {DIFF[s.difficulty][0]}
                          </span>
                        </div>
                        <div style={{ fontSize: 12, color: 'var(--ink-600)', marginTop: 2 }}>
                          Có <span className="mono-num">{s.approvedCount}/{s.requiredCount}</span> câu ·{' '}
                          <strong style={{ color: 'var(--danger-500)' }}>
                            Thiếu {s.missingCount} câu
                          </strong>
                        </div>

                        {/* Inline progress right inside the matrix slot card */}
                        {slotProcessing && (
                          <div style={{ marginTop: 8 }}>
                            <div className="progress-bar">
                              <div
                                className="progress-fill"
                                style={{
                                  width: `${activeJob?.progressPercentage ?? 35}%`,
                                }}
                              />
                            </div>
                            <div style={{ fontSize: 11, color: 'var(--brand-700)', marginTop: 3 }}>
                              Đang truy xuất SGK &amp; sinh bản nháp ({activeJob?.progressPercentage ?? 35}%)…
                            </div>
                          </div>
                        )}
                      </div>
                    </div>
                  </label>
                );
              })}
            </div>
          )}

          <label
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              fontSize: 12,
              color: 'var(--ink-600)',
              marginBottom: 10,
              cursor: 'pointer',
            }}
          >
            <input
              type="checkbox"
              checked={simulateErrorMode}
              disabled={isRunning}
              onChange={e => setSimulateErrorMode(e.target.checked)}
              id="chk-simulate-ai-error"
            />
            Mô phỏng gián đoạn kết nối Bedrock (kiểm tra nút Thử lại)
          </label>

          <button
            className={clsx('btn btn-primary w-full', isRunning && 'btn-loading')}
            disabled={isRunning || selectedSlotIds.length === 0}
            onClick={() => void startGeneration()}
            id="btn-run-generate"
          >
            {isRunning ? <Loader2 size={14} className="spin" /> : <Cpu size={14} />}
            {isRunning ? 'Đang sinh bản thảo gợi ý…' : 'Yêu cầu AI gợi ý câu hỏi mới'}
          </button>

          {activeJob && activeJob.status === 'Failed' && (
            <div className="margin-note red-ink" style={{ marginTop: 12 }}>
              <div style={{ fontWeight: 700 }}>
                Lỗi tác vụ ({activeJob.errorCode ?? 'TIMEOUT'}):
              </div>
              <div style={{ marginTop: 2 }}>{activeJob.safeMessage}</div>
              {activeJob.actionableReason && (
                <div style={{ marginTop: 4, fontSize: 11.5 }}>{activeJob.actionableReason}</div>
              )}
              <button
                className="btn btn-primary btn-sm w-full"
                style={{ marginTop: 8 }}
                onClick={() => void handleRetryJob()}
                id="btn-retry-generate-job"
              >
                <RotateCcw size={12} />
                Thử lại yêu cầu
              </button>
            </div>
          )}

          {isRunning && (
            <button
              className="btn btn-ghost btn-sm w-full"
              style={{ marginTop: 6 }}
              onClick={() => void handleCancelJob()}
              id="btn-cancel-generate-job"
            >
              Dừng yêu cầu đang chạy
            </button>
          )}

          {suggestions.length > 0 && (
            <div
              style={{
                marginTop: 14,
                paddingTop: 12,
                borderTop: '1px solid var(--rule-base)',
                display: 'grid',
                gap: 8,
              }}
            >
              <div style={{ fontSize: 12.5 }}>
                Đã đánh dấu nhận: <strong className="mono-num">{acceptedSuggestions.length}</strong> · Chờ kiểm tra:{' '}
                <strong className="mono-num">{pendingSuggestions.length}</strong>
              </div>
              <button
                className="btn btn-secondary btn-sm"
                disabled={acceptedSuggestions.length === 0}
                onClick={() => commitAccepted(false)}
                id="btn-save-accepted-draft"
              >
                Lưu {acceptedSuggestions.length} câu làm Bản nháp
              </button>
              <button
                className="btn btn-primary btn-sm"
                disabled={acceptedSuggestions.length === 0}
                onClick={() => commitAccepted(true)}
                id="btn-submit-accepted"
              >
                Lưu &amp; Gửi {acceptedSuggestions.length} câu đi thẩm định
              </button>
              {onNavigate && (
                <button
                  className="btn btn-ghost btn-sm"
                  onClick={() => onNavigate('/review')}
                  id="btn-goto-review-from-gen"
                >
                  Sang Bàn duyệt câu hỏi <ArrowRight size={12} />
                </button>
              )}
            </div>
          )}
        </div>

        {/* Right: 2-Column Draft Review Sheets (Question Left | Reference & Check Notes Right) */}
        <div style={{ display: 'grid', gap: 14 }}>
          {suggestions.length === 0 && !isRunning && (
            <div className="table-container">
              <div className="table-empty">
                <div className="table-empty-icon">
                  <Cpu />
                </div>
                <div className="table-empty-text">Chưa có tờ bản thảo gợi ý nào trên bàn</div>
                <div className="table-empty-sub">
                  Chọn ô ma trận thiếu câu bên trái và bấm “Yêu cầu AI gợi ý câu hỏi mới”
                </div>
              </div>
            </div>
          )}

          {isRunning &&
            [0, 1].map(i => (
              <div key={i} className="skeleton" style={{ height: 200 }} />
            ))}

          {suggestions.map(s => {
            const isEditing = editingId === s.id;
            const gate = validateAiDraftSafety({
              stem: s.content,
              options: s.options,
              correctIndex: s.correctIndex,
              explanation: s.explanation,
            });

            return (
              <div
                key={s.id}
                className="draft-review-sheet"
                style={{
                  display: 'grid',
                  gridTemplateColumns: '1.35fr 300px',
                  background: 'var(--paper-sheet)',
                  border: `1px solid ${
                    s.decision === 'ACCEPTED'
                      ? 'var(--brand-600)'
                      : 'var(--rule-strong)'
                  }`,
                  borderRadius: 4,
                  opacity: s.decision === 'DISCARDED' ? 0.55 : 1,
                }}
              >
                {/* Left Column: Draft Question & Options */}
                <div style={{ padding: '16px 20px', borderRight: '1px solid var(--rule-base)' }}>
                  <div style={{ display: 'flex', gap: 6, marginBottom: 10, flexWrap: 'wrap', alignItems: 'center' }}>
                    <span className="badge badge-amber">Gợi ý AI</span>
                    <span className="badge badge-gray">
                      {s.decision === 'ACCEPTED'
                        ? 'Đã kiểm tra · Chờ lưu'
                        : s.decision === 'DISCARDED'
                        ? 'Đã gạch bỏ'
                        : 'Chờ kiểm tra'}
                    </span>
                    <span className="badge badge-gray">
                      {s.subject} / {s.topic}
                    </span>
                    <span className={clsx('badge', DIFF[s.difficulty][1])}>
                      {DIFF[s.difficulty][0]} ({s.points}đ)
                    </span>
                  </div>

                  {isEditing ? (
                    <div style={{ display: 'grid', gap: 8, marginBottom: 10 }}>
                      <label className="form-label" htmlFor={`edit-content-${s.id}`}>
                        Hiệu đính câu dẫn:
                      </label>
                      <textarea
                        className="form-input"
                        rows={2}
                        value={s.content}
                        id={`edit-content-${s.id}`}
                        onChange={e =>
                          setSuggestions(prev =>
                            prev.map(x =>
                              x.id === s.id ? { ...x, content: e.target.value } : x,
                            ),
                          )
                        }
                      />
                      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                        {s.options.map((opt, idx) => (
                          <div key={idx} style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                            <button
                              type="button"
                              className="btn btn-sm mono-num"
                              style={{
                                background:
                                  s.correctIndex === idx ? 'var(--brand-600)' : '#EAE5D9',
                                color: s.correctIndex === idx ? '#FFFFFF' : 'var(--ink-800)',
                              }}
                              onClick={() =>
                                setSuggestions(prev =>
                                  prev.map(x =>
                                    x.id === s.id ? { ...x, correctIndex: idx } : x,
                                  ),
                                )
                              }
                            >
                              {String.fromCharCode(65 + idx)}
                            </button>
                            <input
                              className="form-input"
                              value={opt}
                              onChange={e =>
                                setSuggestions(prev =>
                                  prev.map(x => {
                                    if (x.id !== s.id) return x;
                                    const nextOpts = [...x.options];
                                    nextOpts[idx] = e.target.value;
                                    return { ...x, options: nextOpts };
                                  }),
                                )
                              }
                            />
                          </div>
                        ))}
                      </div>
                    </div>
                  ) : (
                    <>
                      <div
                        style={{
                          fontFamily: 'var(--font-serif)',
                          fontSize: 16.5,
                          fontWeight: 600,
                          color: 'var(--ink-900)',
                          marginBottom: 12,
                          lineHeight: 1.5,
                        }}
                      >
                        {s.content}
                      </div>
                      <div
                        style={{
                          display: 'grid',
                          gridTemplateColumns: '1fr 1fr',
                          gap: 8,
                          marginBottom: 10,
                        }}
                      >
                        {s.options.map((o, i) => (
                          <div
                            key={i}
                            style={{
                              fontSize: 13.5,
                              padding: '7px 10px',
                              borderRadius: 3,
                              border: `1px solid ${
                                i === s.correctIndex ? 'var(--brand-600)' : 'var(--rule-base)'
                              }`,
                              background:
                                i === s.correctIndex ? 'var(--brand-50)' : '#FFFFFF',
                              color:
                                i === s.correctIndex ? 'var(--brand-700)' : 'var(--ink-800)',
                              fontWeight: i === s.correctIndex ? 600 : 400,
                            }}
                          >
                            <strong>{String.fromCharCode(65 + i)}.</strong> {o}
                          </div>
                        ))}
                      </div>
                    </>
                  )}

                  {s.explanation && (
                    <div style={{ fontSize: 12.5, color: 'var(--ink-600)' }}>
                      <strong>Hướng dẫn giải dự kiến:</strong> {s.explanation}
                    </div>
                  )}
                </div>

                {/* Right Column: Source Reference & Verification Notes */}
                <div
                  style={{
                    padding: '14px 16px',
                    background: '#F4F0E5',
                    display: 'flex',
                    flexDirection: 'column',
                    justifyContent: 'space-between',
                    gap: 12,
                  }}
                >
                  <div>
                    <div
                      style={{
                        fontSize: 11,
                        fontWeight: 700,
                        textTransform: 'uppercase',
                        color: 'var(--ink-500)',
                        letterSpacing: '0.05em',
                        marginBottom: 6,
                      }}
                    >
                      Nguồn tham chiếu &amp; Ghi chú rà soát
                    </div>

                    <div
                      style={{
                        padding: '8px 10px',
                        background: 'var(--paper-sheet)',
                        border: '1px solid var(--rule-base)',
                        borderRadius: 3,
                        fontSize: 12,
                        color: 'var(--ink-800)',
                        marginBottom: 8,
                      }}
                    >
                      <div style={{ display: 'flex', gap: 6, alignItems: 'center', fontWeight: 700, marginBottom: 3 }}>
                        <BookMarked size={13} style={{ color: 'var(--brand-600)' }} />
                        <span>{s.citation.source}</span>
                      </div>
                      <div style={{ color: 'var(--ink-600)', marginBottom: 3 }}>
                        Vị trí: <code>{s.citation.locator}</code>
                      </div>
                      <div style={{ fontStyle: 'italic', color: 'var(--ink-700)' }}>
                        “{s.citation.excerpt}”
                      </div>
                    </div>

                    <div
                      className={clsx('margin-note', gate.succeeded ? 'green-ink' : 'red-ink')}
                      style={{ padding: '6px 9px', fontSize: 11.5 }}
                    >
                      {gate.succeeded
                        ? 'Đã qua kiểm tra 4 phương án phân biệt; giáo viên đối chiếu lại kiến thức.'
                        : `Cảnh báo: ${gate.error}`}
                    </div>
                  </div>

                  {!s.decision ? (
                    <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                      <button
                        className="btn btn-ghost btn-sm"
                        style={{ color: 'var(--danger-500)' }}
                        onClick={() => decideSuggestion(s.id, 'DISCARDED')}
                        id={`btn-discard-${s.id}`}
                      >
                        <X size={13} />
                        Bỏ
                      </button>
                      <button
                        className="btn btn-secondary btn-sm"
                        onClick={() => setEditingId(isEditing ? null : s.id)}
                        id={`btn-edit-${s.id}`}
                      >
                        <Pencil size={13} />
                        {isEditing ? 'Xong' : 'Chỉnh câu'}
                      </button>
                      <button
                        className="btn btn-primary btn-sm"
                        disabled={!gate.succeeded}
                        onClick={() => {
                          decideSuggestion(s.id, 'ACCEPTED');
                          setEditingId(null);
                        }}
                        id={`btn-accept-${s.id}`}
                      >
                        <Check size={13} />
                        Nhận vào nháp
                      </button>
                    </div>
                  ) : (
                    <div style={{ textAlign: 'right' }}>
                      <button
                        className="btn btn-secondary btn-sm"
                        onClick={() => decideSuggestion(s.id, undefined)}
                        id={`btn-undo-${s.id}`}
                      >
                        Hoàn tác lựa chọn
                      </button>
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      <style>{`
        .spin { animation: spin 1s linear infinite; }
        @keyframes spin { to { transform: rotate(360deg); } }
        @media (max-width: 1050px) {
          .draft-review-sheet { grid-template-columns: 1fr !important; }
          .gen-grid { grid-template-columns: 1fr !important; }
        }
      `}</style>
    </div>
  );
}
