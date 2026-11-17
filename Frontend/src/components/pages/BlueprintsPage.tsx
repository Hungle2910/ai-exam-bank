'use client';

<<<<<<< HEAD
import React, { useEffect, useState, useCallback, useMemo } from 'react';
import {
  Plus, Trash2, RefreshCw, Save,
  Cpu, BookOpen, FileText, X, Loader2, Check
} from 'lucide-react';
import clsx from 'clsx';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
import {
  BlueprintItem,
  BlueprintSlotItem,
  Difficulty,
  useWorkflowStore,
  computeSlotStats,
  saveBlueprintToStore,
  assembleExamFromBlueprint,
  setWorkflowNavIntent,
  upsertBankQuestion,
} from '@/lib/workflowStore';
import {
  enqueueBackgroundJob,
  fetchJobStatus,
  isTerminalJobStatus,
} from '@/lib/jobClient';

const DIFF_COLS: Array<{ key: Difficulty; label: string; sub: string }> = [
  { key: 'EASY', label: 'Mức 1 · Nhận biết (Dễ)', sub: 'Khái niệm & công thức cơ bản' },
  { key: 'MEDIUM', label: 'Mức 2 · Thông hiểu (TB)', sub: 'Biến đổi & suy luận chuẩn' },
  { key: 'HARD', label: 'Mức 3 · Vận dụng (Khó)', sub: 'Tổng hợp & bài toán tham số' },
];

const DIFF_SHORT: Record<Difficulty, string> = {
  EASY: 'Dễ',
  MEDIUM: 'Trung bình',
  HARD: 'Khó',
};

const round = (n: number) => Math.round(n * 100) / 100;
const sumPoints = (s: BlueprintSlotItem[]) =>
  round(s.reduce((a, x) => a + x.questionCount * x.pointsPerQuestion, 0));
const sumQuestions = (s: BlueprintSlotItem[]) => s.reduce((a, x) => a + x.questionCount, 0);

export default function BlueprintsPage({ onNavigate }: { onNavigate?: (href: string) => void }) {
  const { user, primaryRole, csrfToken } = useAuth();
  const { success, error: showError } = useToast();
  const wf = useWorkflowStore();

  const [apiList, setApiList] = useState<BlueprintItem[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeBpId, setActiveBpId] = useState<string>(() => wf.blueprints[0]?.id ?? 'bp-1');
  const [draftBp, setDraftBp] = useState<BlueprintItem | null>(null);

  // Selected cell in the 2D matrix: { topic, difficulty }
  const [selectedCell, setSelectedCell] = useState<{
    topic: string;
    difficulty: Difficulty;
  } | null>({ topic: 'Đạo hàm', difficulty: 'MEDIUM' });
  const [mobilePanelOpen, setMobilePanelOpen] = useState(false);

  // Inline AI generation progress per cell key `${topic}::${difficulty}`
  const [cellJobProgress, setCellJobProgress] = useState<Record<string, number>>({});
  const [newTopicName, setNewTopicName] = useState('');
  const [addingTopic, setAddingTopic] = useState(false);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    try {
      const res = await fetch('/api/v1/blueprints', { credentials: 'include' });
      if (res.ok) {
        const data = (await res.json()) as BlueprintItem[];
        if (Array.isArray(data) && data.length > 0) {
          setApiList(data);
          return;
        }
      }
      setApiList(null);
    } catch {
      setApiList(null);
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

  const list = apiList ?? wf.blueprints;
  const isMock = apiList === null;
  const canAuthor = primaryRole === 'Teacher' || primaryRole === 'DepartmentHead';

  const currentBp = useMemo(() => {
    if (draftBp && draftBp.id === activeBpId) return draftBp;
    return list.find(b => b.id === activeBpId) ?? list[0] ?? null;
  }, [draftBp, activeBpId, list]);

  // Distinct topics in the current blueprint
  const topics = useMemo(() => {
    if (!currentBp) return [];
    const seen = new Set<string>();
    const out: string[] = [];
    currentBp.slots.forEach(s => {
      const t = s.topic.trim() || 'Chưa đặt tên';
      if (!seen.has(t)) {
        seen.add(t);
        out.push(t);
      }
    });
    return out;
  }, [currentBp]);

  if (loading) {
    return (
      <div style={{ display: 'grid', gap: 14 }}>
        <div className="skeleton" style={{ height: 54 }} />
        <div className="skeleton" style={{ height: 340 }} />
      </div>
    );
  }

  if (!currentBp) {
    return (
      <div className="table-container">
        <div className="table-empty">
          <div className="table-empty-icon"><FileText /></div>
          <div className="table-empty-text">Chưa có ma trận đề nào trên bàn soạn đề</div>
        </div>
      </div>
    );
  }

  const updateCurrentBp = (updater: (prev: BlueprintItem) => BlueprintItem) => {
    const next = updater(currentBp);
    setDraftBp(next);
    saveBlueprintToStore(next);
  };

  const getSlot = (topic: string, difficulty: Difficulty): BlueprintSlotItem | undefined =>
    currentBp.slots.find(
      s => s.topic.toLowerCase() === topic.toLowerCase() && s.difficulty === difficulty,
    );

  const pts = sumPoints(currentBp.slots);
  const totalReq = sumQuestions(currentBp.slots);
  const slotDiagnostics = currentBp.slots.map(s => ({
    slot: s,
    stats: computeSlotStats(s, wf.questions),
  }));
  const missingSlots = slotDiagnostics.filter(d => d.stats.missingCount > 0);
  const totalMissing = missingSlots.reduce((acc, d) => acc + d.stats.missingCount, 0);
  const totalApprovedAvailable = slotDiagnostics.reduce(
    (acc, d) => acc + Math.min(d.slot.questionCount, d.stats.approvedCount),
    0,
  );

  const handleSelectBlueprint = (id: string) => {
    setActiveBpId(id);
    setDraftBp(null);
    const found = list.find(b => b.id === id);
    if (found && found.slots[0]) {
      setSelectedCell({ topic: found.slots[0].topic, difficulty: found.slots[0].difficulty });
    }
  };

  const handleCreateNewBlueprint = () => {
    const created: BlueprintItem = {
      id: `bp-${Date.now().toString(36)}`,
      name: `Ma trận đề kiểm tra ${currentBp.subject} mới`,
      subject: currentBp.subject,
      grade: 'Khối 12',
      totalPoints: 2,
      durationMinutes: 45,
      author: user?.userName ?? 'gv.nguyen.van.a',
      updatedAt: new Date().toISOString(),
      slots: [
        {
          id: `s-${Date.now()}-1`,
          subject: currentBp.subject,
          topic: 'Đạo hàm',
          difficulty: 'EASY',
          questionCount: 2,
          pointsPerQuestion: 0.25,
        },
        {
          id: `s-${Date.now()}-2`,
          subject: currentBp.subject,
          topic: 'Đạo hàm',
          difficulty: 'MEDIUM',
          questionCount: 2,
          pointsPerQuestion: 0.25,
        },
        {
          id: `s-${Date.now()}-3`,
          subject: currentBp.subject,
          topic: 'Logarit',
          difficulty: 'MEDIUM',
          questionCount: 2,
          pointsPerQuestion: 0.25,
        },
        {
          id: `s-${Date.now()}-4`,
          subject: currentBp.subject,
          topic: 'Logarit',
          difficulty: 'HARD',
          questionCount: 1,
          pointsPerQuestion: 0.5,
        },
      ],
    };
    saveBlueprintToStore(created);
    setActiveBpId(created.id);
    setDraftBp(created);
    setSelectedCell({ topic: 'Đạo hàm', difficulty: 'MEDIUM' });
    success('Đã mở tờ ma trận đề mới trên bàn soạn đề');
  };

  const handleSaveBlueprint = async () => {
    setSaving(true);
    try {
      const saved = { ...currentBp, updatedAt: new Date().toISOString() };
      if (!isMock && csrfToken) {
        const res = await fetch(`/api/v1/blueprints/${encodeURIComponent(saved.id)}`, {
          method: 'PUT',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken },
          body: JSON.stringify(saved),
        });
        if (!res.ok) {
          showError('Không thể lưu ma trận lên máy chủ');
          return;
        }
      }
      saveBlueprintToStore(saved);
      success('Đã lưu tờ ma trận đặc tả đề thi');
    } finally {
      setSaving(false);
    }
  };

  const handleAssemble = () => {
    const actor = user?.userName ?? 'gv.nguyen.van.a';
    const { exam, ok } = assembleExamFromBlueprint(currentBp, actor, 'SCHOOL');
    success(
      ok
        ? `Đã ráp đề "${exam.title}" đủ câu hỏi đạt chuẩn`
        : `Đã tạo bản ráp "${exam.title}" (còn ô thiếu câu hỏi)`,
    );
    onNavigate?.('/exams');
  };

  const handleQuickInlineAi = async (slot: BlueprintSlotItem) => {
    const cellKey = `${slot.topic}::${slot.difficulty}`;
    if (cellJobProgress[cellKey] !== undefined) return;
    setCellJobProgress(prev => ({ ...prev, [cellKey]: 20 }));

    const { job } = await enqueueBackgroundJob({
      jobType: 'ExamGeneration',
      payloadReference: `slot:${slot.id}:${slot.topic}:${slot.difficulty}`,
      idempotencyKey: `inline-gen-${slot.id}-${Date.now()}`,
    });

    let cur = job;
    for (let i = 0; i < 4 && !isTerminalJobStatus(cur.status); i++) {
      await new Promise(r => setTimeout(r, 220));
      const next = await fetchJobStatus(cur.jobId);
      if (next) {
        cur = next;
        setCellJobProgress(prev => ({
          ...prev,
          [cellKey]: next.progressPercentage ?? (i + 2) * 25,
        }));
      }
    }

    // Create an AI draft question in the store for this missing slot
    upsertBankQuestion({
      content: `Cho hàm số liên quan chủ đề ${slot.topic} (${DIFF_SHORT[slot.difficulty]}). Khẳng định nào sau đây là đúng theo định lý SGK?`,
      options: [
        'Hàm số thỏa mãn điều kiện cực trị và đơn điệu trên miền xác định',
        'Hàm số không có đạo hàm tại mọi điểm thuộc tập xác định',
        'Giá trị biểu thức luôn âm với mọi tham số thực',
        'Phương trình tương ứng vô nghiệm trên tập số thực',
      ],
      correctIndex: 0,
      explanation: `Sinh tự động theo yêu cầu bổ sung ô ${slot.topic} (${DIFF_SHORT[slot.difficulty]}).`,
      subject: slot.subject,
      topic: slot.topic,
      difficulty: slot.difficulty,
      points: slot.pointsPerQuestion,
      source: 'AI_DRAFT',
      actorUserName: user?.userName ?? 'gv.nguyen.van.a',
      submitForReview: false,
      citation: {
        sourceId: 'kb-math12-ch1',
        source: `SGK ${slot.subject} 12 — Chuyên đề ${slot.topic}`,
        locator: 'Mục 1.2, tr. 14',
        excerpt: 'Định lý cơ bản và các bước khảo sát sự biến thiên của hàm số.',
      },
      note: `Gợi ý AI khởi tạo trực tiếp từ ô ma trận ${slot.topic} (${DIFF_SHORT[slot.difficulty]})`,
    });

    setCellJobProgress(prev => {
      const next = { ...prev };
      delete next[cellKey];
      return next;
    });
    success(`Đã tạo 1 bản nháp Gợi ý AI cho ô "${slot.topic} (${DIFF_SHORT[slot.difficulty]})"`);
  };

  const handleAddTopicRow = () => {
    const trimmed = newTopicName.trim();
    if (!trimmed) return;
    const newSlot: BlueprintSlotItem = {
      id: `s-${Date.now()}`,
      subject: currentBp.subject,
      topic: trimmed,
      difficulty: 'EASY',
      questionCount: 2,
      pointsPerQuestion: 0.25,
    };
    updateCurrentBp(prev => ({
      ...prev,
      slots: [...prev.slots, newSlot],
    }));
    setSelectedCell({ topic: trimmed, difficulty: 'EASY' });
    setNewTopicName('');
    setAddingTopic(false);
  };

  const activeSlot = selectedCell
    ? getSlot(selectedCell.topic, selectedCell.difficulty)
    : undefined;
  const activeSlotStats = activeSlot
    ? computeSlotStats(activeSlot, wf.questions)
    : null;

  return (
    <div>
      {/* Editorial Document Title Bar */}
      <div className="page-header">
        <div>
          <h1 className="page-title">{currentBp.name}</h1>
          <p className="page-subtitle">
            Ma trận đặc tả môn {currentBp.subject} ({currentBp.grade}). Bấm trực tiếp vào từng ô để đối chiếu câu hỏi hoặc yêu cầu AI gợi ý bổ sung.
          </p>
        </div>
        <div className="page-actions" style={{ flexWrap: 'wrap' }}>
          <select
            className="form-select"
            style={{ width: 'auto', minWidth: 230, fontWeight: 600 }}
            value={currentBp.id}
            onChange={e => handleSelectBlueprint(e.target.value)}
            aria-label="Chọn hồ sơ ma trận đề"
            id="blueprint-switcher"
          >
            {list.map(b => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
          {canAuthor && (
            <>
              <button
                className="btn btn-secondary"
                onClick={handleCreateNewBlueprint}
                id="btn-create-blueprint"
              >
                <Plus size={14} />
                Tờ ma trận mới
              </button>
              <button
                className={clsx('btn btn-secondary', saving && 'btn-loading')}
                disabled={saving}
                onClick={() => void handleSaveBlueprint()}
                id="btn-save-blueprint"
              >
                <Save size={14} />
                Lưu ma trận
              </button>
              <button
                className="btn btn-primary"
                onClick={handleAssemble}
                id={`btn-assemble-${currentBp.id}`}
              >
                <FileText size={14} />
                Ráp đề &amp; Xem bản in
              </button>
            </>
          )}
          <button
            className="btn btn-ghost btn-sm"
            onClick={() => void load()}
            id="btn-refresh-blueprints"
            title="Làm mới"
          >
            <RefreshCw size={14} />
          </button>
        </div>
      </div>

      {/* Documentary Ledger Strip (No decorative KPI cards) */}
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: '10px 24px',
          padding: '10px 16px',
          background: '#EFECE1',
          border: '1px solid var(--rule-base)',
          borderRadius: 4,
          marginBottom: 16,
          fontSize: 13,
          alignItems: 'center',
        }}
      >
        <div>
          <span style={{ color: 'var(--ink-600)' }}>Môn học:</span>{' '}
          <strong>{currentBp.subject}</strong>
        </div>
        <div>
          <span style={{ color: 'var(--ink-600)' }}>Thời lượng:</span>{' '}
          <strong className="mono-num">{currentBp.durationMinutes} phút</strong>
        </div>
        <div>
          <span style={{ color: 'var(--ink-600)' }}>Số câu đã duyệt / Yêu cầu:</span>{' '}
          <strong className="mono-num">
            {totalApprovedAvailable}/{totalReq} câu
          </strong>
        </div>
        <div>
          <span style={{ color: 'var(--ink-600)' }}>Tổng điểm ma trận:</span>{' '}
          <strong
            className="mono-num"
            style={{
              color: pts === currentBp.totalPoints ? 'var(--brand-600)' : 'var(--danger-500)',
            }}
          >
            {pts.toFixed(2)} / {currentBp.totalPoints.toFixed(2)} điểm
          </strong>
        </div>
        <div style={{ marginLeft: 'auto' }}>
          {totalMissing > 0 ? (
            <span className="badge badge-red">
              Còn thiếu {totalMissing} câu ở {missingSlots.length} ô đặc tả
            </span>
          ) : (
            <span className="badge badge-green">
              Đủ câu hỏi đạt chuẩn để chốt đề
            </span>
          )}
        </div>
      </div>

      {/* Main Authoring Desk: Central 2D Matrix + Sticky Side Workbench */}
      <div
        className="blueprint-desk-layout"
        style={{
          display: 'grid',
          gridTemplateColumns: '1fr 370px',
          gap: 18,
          alignItems: 'start',
        }}
      >
        {/* Left / Center: 2D Matrix Table */}
        <div className="table-container">
          <div className="table-toolbar" style={{ justifyContent: 'space-between', flexWrap: 'wrap' }}>
            <div>
              <strong style={{ fontFamily: 'var(--font-serif)', fontSize: 15 }}>
                Bảng Ma trận Đặc tả 2 chiều (Chủ đề × Mức độ nhận thức)
              </strong>
              <span style={{ fontSize: 12, color: 'var(--ink-500)', marginLeft: 10 }}>
                Ô có dấu góc đỏ là ô còn thiếu câu hỏi đã duyệt
              </span>
            </div>
            {canAuthor && (
              <div>
                {addingTopic ? (
                  <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                    <input
                      className="form-input"
                      style={{ width: 170, height: 28, fontSize: 12.5 }}
                      placeholder="Tên chủ đề mới…"
                      value={newTopicName}
                      onChange={e => setNewTopicName(e.target.value)}
                      onKeyDown={e => {
                        if (e.key === 'Enter') handleAddTopicRow();
                        if (e.key === 'Escape') setAddingTopic(false);
                      }}
                      autoFocus
                    />
                    <button className="btn btn-primary btn-sm" onClick={handleAddTopicRow}>
                      Thêm
                    </button>
                    <button className="btn btn-ghost btn-sm" onClick={() => setAddingTopic(false)}>
                      Hủy
                    </button>
                  </div>
                ) : (
                  <button
                    className="btn btn-secondary btn-sm"
                    onClick={() => setAddingTopic(true)}
                    id="btn-add-topic-row"
                  >
                    <Plus size={12} />
                    Thêm dòng chủ đề
                  </button>
                )}
              </div>
            )}
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table className="matrix-grid-table">
              <thead>
                <tr>
                  <th style={{ width: 190 }}>Mạch kiến thức / Chủ đề</th>
                  {DIFF_COLS.map(col => (
                    <th key={col.key}>
                      <div>{col.label}</div>
                      <div
                        style={{
                          fontSize: 10.5,
                          fontWeight: 400,
                          textTransform: 'none',
                          color: 'var(--ink-500)',
                          letterSpacing: 0,
                        }}
                      >
                        {col.sub}
                      </div>
                    </th>
                  ))}
                  <th style={{ width: 115, textAlign: 'right' }}>Tổng chủ đề</th>
                </tr>
              </thead>
              <tbody>
                {topics.map(topic => {
                  const rowSlots = currentBp.slots.filter(
                    s => s.topic.toLowerCase() === topic.toLowerCase(),
                  );
                  const rowReq = sumQuestions(rowSlots);
                  const rowPts = sumPoints(rowSlots);

                  return (
                    <tr key={topic}>
                      <td
                        style={{
                          padding: '12px 14px',
                          background: '#F7F4EC',
                          fontWeight: 700,
                          color: 'var(--ink-900)',
                          verticalAlign: 'top',
                        }}
                      >
                        <div style={{ fontFamily: 'var(--font-serif)', fontSize: 14.5 }}>
                          {topic}
                        </div>
                        <div style={{ fontSize: 11.5, color: 'var(--ink-500)', fontWeight: 400, marginTop: 2 }}>
                          Môn {currentBp.subject}
                        </div>
                      </td>

                      {DIFF_COLS.map(col => {
                        const cellSlot = getSlot(topic, col.key);
                        const stats = cellSlot
                          ? computeSlotStats(cellSlot, wf.questions)
                          : null;
                        const isSelected =
                          selectedCell?.topic.toLowerCase() === topic.toLowerCase() &&
                          selectedCell?.difficulty === col.key;
                        const isMissing = Boolean(stats && stats.missingCount > 0);
                        const cellKey = `${topic}::${col.key}`;
                        const runningProgress = cellJobProgress[cellKey];

                        if (!cellSlot) {
                          return (
                            <td key={col.key}>
                              <button
                                type="button"
                                className={clsx('matrix-cell-btn', isSelected && 'selected')}
                                style={{
                                  background: isSelected ? '#EBF3F1' : '#FAF8F2',
                                  color: 'var(--ink-400)',
                                  justifyContent: 'center',
                                  alignItems: 'center',
                                }}
                                onClick={() => {
                                  setSelectedCell({ topic, difficulty: col.key });
                                  setMobilePanelOpen(true);
                                }}
                                id={`cell-empty-${topic}-${col.key}`}
                              >
                                <span style={{ fontSize: 12 }}>Không lấy câu</span>
                                {canAuthor && (
                                  <span style={{ fontSize: 11, color: 'var(--brand-600)', marginTop: 2 }}>
                                    + Bấm để định nghĩa ô
                                  </span>
                                )}
                              </button>
                            </td>
                          );
                        }

                        return (
                          <td key={col.key}>
                            <button
                              type="button"
                              className={clsx(
                                'matrix-cell-btn',
                                isSelected && 'selected',
                                isMissing && 'missing',
                              )}
                              onClick={() => {
                                setSelectedCell({ topic, difficulty: col.key });
                                setMobilePanelOpen(true);
                              }}
                              id={
                                cellSlot.id === 's2'
                                  ? `blueprint-${currentBp.id}`
                                  : `matrix-cell-${cellSlot.id}`
                              }
                            >
                              {isMissing && (
                                <span
                                  className="matrix-missing-corner"
                                  title={`Thiếu ${stats?.missingCount} câu đã duyệt`}
                                />
                              )}

                              <div
                                style={{
                                  display: 'flex',
                                  justifyContent: 'space-between',
                                  alignItems: 'baseline',
                                  width: '100%',
                                  gap: 6,
                                }}
                              >
                                <span
                                  className="mono-num"
                                  style={{
                                    fontSize: 15,
                                    fontWeight: 700,
                                    color: isMissing ? 'var(--danger-500)' : 'var(--ink-900)',
                                  }}
                                >
                                  {stats?.approvedCount ?? 0}/{cellSlot.questionCount} câu
                                </span>
                                <span
                                  className="mono-num"
                                  style={{ fontSize: 12, color: 'var(--ink-600)' }}
                                >
                                  {round(cellSlot.questionCount * cellSlot.pointsPerQuestion)}đ
                                </span>
                              </div>

                              {runningProgress !== undefined ? (
                                <div style={{ width: '100%', marginTop: 6 }}>
                                  <div className="progress-bar">
                                    <div
                                      className="progress-fill"
                                      style={{ width: `${runningProgress}%` }}
                                    />
                                  </div>
                                  <div style={{ fontSize: 11, color: 'var(--brand-600)', marginTop: 2 }}>
                                    Đang tạo gợi ý AI ({runningProgress}%)…
                                  </div>
                                </div>
                              ) : (
                                <div
                                  style={{
                                    display: 'flex',
                                    justifyContent: 'space-between',
                                    alignItems: 'center',
                                    width: '100%',
                                    marginTop: 8,
                                    gap: 4,
                                  }}
                                >
                                  {isMissing ? (
                                    <span
                                      className="badge badge-red"
                                      style={{ fontSize: 11, padding: '0 6px' }}
                                    >
                                      Thiếu {stats?.missingCount} câu
                                    </span>
                                  ) : (
                                    <span
                                      className="badge badge-green"
                                      style={{ fontSize: 11, padding: '0 6px' }}
                                    >
                                      Đủ câu ({cellSlot.pointsPerQuestion}đ/câu)
                                    </span>
                                  )}
                                  {(stats?.pendingCount ?? 0) > 0 && (
                                    <span style={{ fontSize: 11, color: 'var(--warning-600)' }}>
                                      +{stats?.pendingCount} chờ duyệt
                                    </span>
                                  )}
                                </div>
                              )}
                            </button>
                          </td>
                        );
                      })}

                      <td
                        style={{
                          padding: '10px 12px',
                          textAlign: 'right',
                          background: '#F7F4EC',
                          verticalAlign: 'middle',
                        }}
                      >
                        <div className="mono-num" style={{ fontWeight: 700, fontSize: 14 }}>
                          {rowReq} câu
                        </div>
                        <div className="mono-num" style={{ fontSize: 12, color: 'var(--ink-600)' }}>
                          {rowPts.toFixed(2)} điểm
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
              <tfoot>
                <tr style={{ background: '#EAE5D7', borderTop: '2px solid var(--ink-900)' }}>
                  <td style={{ fontWeight: 700, padding: '10px 14px' }}>
                    Tổng cộng theo mức độ
                  </td>
                  {DIFF_COLS.map(col => {
                    const colSlots = currentBp.slots.filter(s => s.difficulty === col.key);
                    const colReq = sumQuestions(colSlots);
                    const colPts = sumPoints(colSlots);
                    return (
                      <td key={col.key} style={{ padding: '10px 12px', fontWeight: 700 }}>
                        <span className="mono-num">{colReq} câu</span>{' '}
                        <span className="mono-num" style={{ color: 'var(--ink-600)', fontWeight: 500 }}>
                          ({colPts.toFixed(2)}đ)
                        </span>
                      </td>
                    );
                  })}
                  <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 700 }}>
                    <div className="mono-num">{totalReq} câu</div>
                    <div className="mono-num" style={{ color: 'var(--brand-700)' }}>
                      {pts.toFixed(2)}đ
                    </div>
                  </td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>

        {/* Right: Sticky Side Workbench Panel (Desktop inline, Mobile full-screen drawer) */}
        <div
          className={clsx('card workbench-side-panel', mobilePanelOpen && 'mobile-open')}
          style={{ position: 'sticky', top: 64 }}
        >
          <div className="card-header">
            <div>
              <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--ink-500)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                Khung thao tác ô ma trận
              </div>
              <div className="card-title">
                {selectedCell
                  ? `${selectedCell.topic} · ${DIFF_SHORT[selectedCell.difficulty]}`
                  : 'Chọn một ô trên bảng ma trận'}
              </div>
            </div>
            <button
              className="topbar-icon-btn mobile-close-workbench"
              style={{ display: 'none' }}
              onClick={() => setMobilePanelOpen(false)}
              aria-label="Đóng khung thao tác"
            >
              <X size={15} />
            </button>
          </div>

          <div className="card-body">
            {!selectedCell ? (
              <div style={{ fontSize: 13, color: 'var(--ink-500)' }}>
                Bấm vào bất kỳ ô nào trong bảng ma trận bên trái để xem danh sách câu hỏi hoặc yêu cầu AI gợi ý.
              </div>
            ) : !activeSlot ? (
              <div>
                <p style={{ fontSize: 13, color: 'var(--ink-600)', marginBottom: 12 }}>
                  Ô <strong>{selectedCell.topic}</strong> ở mức độ{' '}
                  <strong>{DIFF_SHORT[selectedCell.difficulty]}</strong> hiện chưa lấy câu hỏi nào trong ma trận.
                </p>
                {canAuthor && (
                  <button
                    className="btn btn-primary w-full"
                    onClick={() => {
                      const created: BlueprintSlotItem = {
                        id: `s-${Date.now()}`,
                        subject: currentBp.subject,
                        topic: selectedCell.topic,
                        difficulty: selectedCell.difficulty,
                        questionCount: 2,
                        pointsPerQuestion: 0.25,
                      };
                      updateCurrentBp(prev => ({
                        ...prev,
                        slots: [...prev.slots, created],
                      }));
                    }}
                    id="btn-define-slot"
                  >
                    <Plus size={14} />
                    Định nghĩa ô này (2 câu × 0.25đ)
                  </button>
                )}
              </div>
            ) : (
              <>
                {/* Status callout for this slot */}
                {activeSlotStats && activeSlotStats.missingCount > 0 ? (
                  <div className="margin-note red-ink" style={{ marginBottom: 14 }}>
                    <strong>Thiếu {activeSlotStats.missingCount} câu đã duyệt:</strong> Ô này yêu cầu{' '}
                    <span className="mono-num">{activeSlot.questionCount}</span> câu nhưng ngân hàng chỉ có{' '}
                    <span className="mono-num">{activeSlotStats.approvedCount}</span> câu ở trạng thái Đã duyệt.
                  </div>
                ) : (
                  <div className="margin-note green-ink" style={{ marginBottom: 14 }}>
                    <strong>Đạt yêu cầu:</strong> Đã có đủ{' '}
                    <span className="mono-num">
                      {activeSlotStats?.approvedCount}/{activeSlot.questionCount}
                    </span>{' '}
                    câu hỏi đã duyệt cho ô này.
                  </div>
                )}

                {/* Inline slot constraint controls */}
                {canAuthor && (
                  <div
                    style={{
                      display: 'grid',
                      gridTemplateColumns: '1fr 1fr auto',
                      gap: 8,
                      alignItems: 'end',
                      marginBottom: 14,
                      paddingBottom: 14,
                      borderBottom: '1px solid var(--rule-light)',
                    }}
                  >
                    <div>
                      <label className="form-label" htmlFor="wb-slot-count">
                        Số câu yêu cầu
                      </label>
                      <input
                        id="wb-slot-count"
                        type="number"
                        min={1}
                        className="form-input mono-num"
                        value={activeSlot.questionCount}
                        onChange={e =>
                          updateCurrentBp(prev => ({
                            ...prev,
                            slots: prev.slots.map(s =>
                              s.id === activeSlot.id
                                ? { ...s, questionCount: Math.max(1, Number(e.target.value)) }
                                : s,
                            ),
                          }))
                        }
                      />
                    </div>
                    <div>
                      <label className="form-label" htmlFor="wb-slot-pts">
                        Điểm / câu
                      </label>
                      <input
                        id="wb-slot-pts"
                        type="number"
                        min={0.05}
                        step={0.05}
                        className="form-input mono-num"
                        value={activeSlot.pointsPerQuestion}
                        onChange={e =>
                          updateCurrentBp(prev => ({
                            ...prev,
                            slots: prev.slots.map(s =>
                              s.id === activeSlot.id
                                ? { ...s, pointsPerQuestion: Math.max(0.05, Number(e.target.value)) }
                                : s,
                            ),
                          }))
                        }
                      />
                    </div>
                    <button
                      className="btn btn-ghost btn-sm"
                      style={{ color: 'var(--danger-500)', height: 33 }}
                      onClick={() =>
                        updateCurrentBp(prev => ({
                          ...prev,
                          slots: prev.slots.filter(s => s.id !== activeSlot.id),
                        }))
                      }
                      title="Xóa ô khỏi ma trận"
                    >
                      <Trash2 size={14} />
                    </button>
                  </div>
                )}

                {/* Matching Questions in Bank for this cell */}
                <div
                  style={{
                    fontSize: 11.5,
                    fontWeight: 700,
                    color: 'var(--ink-600)',
                    textTransform: 'uppercase',
                    letterSpacing: '0.04em',
                    marginBottom: 8,
                  }}
                >
                  Câu hỏi khớp ô này trong mục lục ({activeSlotStats?.allMatchingQuestions.length ?? 0})
                </div>

                {!activeSlotStats || activeSlotStats.allMatchingQuestions.length === 0 ? (
                  <div
                    style={{
                      padding: 12,
                      background: '#F4F1E8',
                      border: '1px dashed var(--rule-strong)',
                      borderRadius: 3,
                      fontSize: 12.5,
                      color: 'var(--ink-600)',
                      marginBottom: 14,
                    }}
                  >
                    Chưa có câu hỏi nào thuộc chủ đề <strong>{activeSlot.topic}</strong> (
                    {DIFF_SHORT[activeSlot.difficulty]}).
                  </div>
                ) : (
                  <div
                    style={{
                      display: 'grid',
                      gap: 8,
                      maxHeight: 230,
                      overflowY: 'auto',
                      marginBottom: 14,
                    }}
                  >
                    {activeSlotStats.allMatchingQuestions.map(q => (
                      <div
                        key={q.id}
                        style={{
                          padding: '8px 10px',
                          background: q.status === 'APPROVED' ? '#F3F8F6' : '#FAF8F2',
                          border: `1px solid ${
                            q.status === 'APPROVED' ? 'var(--brand-200)' : 'var(--rule-base)'
                          }`,
                          borderRadius: 3,
                          fontSize: 12.5,
                        }}
                      >
                        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 3 }}>
                          <code style={{ fontSize: 11, color: 'var(--ink-600)' }}>
                            {q.id} · rev {q.revision}
                          </code>
                          <span
                            className={clsx(
                              'badge',
                              q.status === 'APPROVED'
                                ? 'badge-green'
                                : q.status === 'REVIEW_REQUIRED'
                                ? 'badge-amber'
                                : 'badge-gray',
                            )}
                            style={{ fontSize: 10.5, padding: '0 5px' }}
                          >
                            {q.status === 'APPROVED' ? (
                              <>
                                <Check size={10} /> Đã duyệt
                              </>
                            ) : q.status === 'REVIEW_REQUIRED' ? (
                              'Chờ duyệt'
                            ) : (
                              'Bản nháp'
                            )}
                          </span>
                        </div>
                        <div style={{ color: 'var(--ink-900)', fontWeight: 500, lineHeight: 1.4 }}>
                          {q.content}
                        </div>
                      </div>
                    ))}
                  </div>
                )}

                {/* Workbench Actions: Open in Question Index or Request AI Suggestion */}
                <div style={{ display: 'grid', gap: 8 }}>
                  {canAuthor && (
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                      <button
                        type="button"
                        className="btn btn-secondary btn-sm"
                        disabled={
                          cellJobProgress[`${activeSlot.topic}::${activeSlot.difficulty}`] !==
                          undefined
                        }
                        onClick={() => void handleQuickInlineAi(activeSlot)}
                        id="btn-inline-ai-slot"
                      >
                        {cellJobProgress[`${activeSlot.topic}::${activeSlot.difficulty}`] !==
                        undefined ? (
                          <Loader2 size={13} className="spin" />
                        ) : (
                          <Cpu size={13} />
                        )}
                        Tạo nháp nhanh tại ô
                      </button>

                      <button
                        type="button"
                        className="btn btn-primary btn-sm"
                        onClick={() => {
                          setWorkflowNavIntent({
                            blueprintId: currentBp.id,
                            slotIds: [activeSlot.id],
                            subject: activeSlot.subject,
                            topic: activeSlot.topic,
                            difficulty: activeSlot.difficulty,
                          });
                          onNavigate?.('/generate');
                        }}
                        id="btn-slot-ask-ai"
                      >
                        <Cpu size={13} />
                        Mở bàn rà soát gợi ý AI
                      </button>
                    </div>
                  )}

                  <button
                    type="button"
                    className="btn btn-secondary btn-sm w-full"
                    onClick={() => {
                      setWorkflowNavIntent({
                        blueprintId: currentBp.id,
                        slotIds: [activeSlot.id],
                        subject: activeSlot.subject,
                        topic: activeSlot.topic,
                        difficulty: activeSlot.difficulty,
                      });
                      onNavigate?.('/questions');
                    }}
                    id="btn-slot-open-bank"
                  >
                    <BookOpen size={13} />
                    Tìm trong Mục lục câu hỏi ({activeSlot.topic})
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      </div>

      <style>{`
        .spin { animation: spin 1s linear infinite; }
        @keyframes spin { to { transform: rotate(360deg); } }
        @media (max-width: 960px) {
          .blueprint-desk-layout {
            grid-template-columns: 1fr !important;
          }
          .workbench-side-panel {
            display: none;
          }
          .workbench-side-panel.mobile-open {
            display: block !important;
            position: fixed !important;
            inset: 0 !important;
            z-index: 120 !important;
            border-radius: 0 !important;
            overflow-y: auto !important;
          }
          .mobile-close-workbench {
            display: inline-flex !important;
          }
        }
      `}</style>
    </div>
  );
=======
import React, { useCallback, useEffect, useState } from 'react';
import { useAuth } from '@/contexts/AuthContext';

type Slot = { topicId: string; difficulty: string; questionCount: number; pointsPerQuestion: number };
type Blueprint = { id?: string; name: string; subjectId: string; durationMinutes: number; totalQuestions: number; totalPoints: number; slots: Slot[]; version?: string; archivedAt?: string | null };
type Subject = { id: string; name: string; canWrite: boolean; topics: { id: string; name: string }[] };
const empty = (): Blueprint => ({ name: '', subjectId: '', durationMinutes: 45, totalQuestions: 10, totalPoints: 10, slots: [] });

export default function BlueprintsPage() {
  const { apiFetch, csrfToken } = useAuth();
  const [items, setItems] = useState<Blueprint[]>([]);
  const [catalog, setCatalog] = useState<Subject[]>([]);
  const [editing, setEditing] = useState<Blueprint | null>(null);
  const [error, setError] = useState('');
  const [fields, setFields] = useState<Record<string, string[]>>({});
  const [busy, setBusy] = useState(true);
  const [conflict, setConflict] = useState(false);
  const [page, setPage] = useState(1);
  const [archived, setArchived] = useState(false);
  const [total, setTotal] = useState(0);
  const [notice, setNotice] = useState('');
  const load = useCallback(async () => {
    setBusy(true);
    try {
      const [list, subjects] = await Promise.all([apiFetch(`/api/v1/blueprints?page=${page}&pageSize=20&archived=${archived}`), apiFetch('/api/v1/catalog')]);
      if (!list.ok || !subjects.ok) throw new Error('Không tải được ma trận. Kiểm tra phiên đăng nhập và thử lại.');
      const data = await list.json();
      setItems(data.items); setTotal(data.total); setCatalog(await subjects.json()); setError('');
    } catch (e) { setError(e instanceof Error ? e.message : 'Không kết nối được máy chủ.'); }
    finally { setBusy(false); }
  }, [apiFetch, page, archived]);
  useEffect(() => {
    // load updates state after the network request settles, including its error path.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  function edit(value: Blueprint) { setEditing(structuredClone(value)); setFields({}); setError(''); setConflict(false); setNotice(''); }
  async function save(e: React.FormEvent) {
    e.preventDefault();
    if (!editing || editing.archivedAt || busy) return;
    if (!csrfToken) { setError('Chưa lấy được token bảo vệ phiên. Tải lại trang trước khi lưu.'); return; }
    setBusy(true); setError(''); setFields({}); setConflict(false);
    try {
      const response = await apiFetch(`/api/v1/blueprints${editing.id ? `/${editing.id}` : ''}`, {
        method: editing.id ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken }, body: JSON.stringify(editing),
      });
      if (response.status === 409) { setConflict(true); setError('Ma trận đã có phiên bản mới. Nội dung bạn đang nhập được giữ lại.'); return; }
      if (!response.ok) {
        const problem = await response.json().catch(() => null);
        setFields(problem?.errors ?? {});
        setError(response.status === 403 ? 'Bạn không có quyền sửa ma trận này.' : problem?.title ?? 'Không lưu được ma trận.');
        return;
      }
      const saved: Blueprint = await response.json();
      setEditing(saved); setNotice('Đã lưu ma trận trên máy chủ.');
    } catch { setError('Không kết nối được máy chủ. Nội dung đang nhập vẫn được giữ lại.'); }
    finally { setBusy(false); }
  }
  async function reload() {
    if (!editing?.id || !window.confirm('Tải bản mới sẽ thay nội dung chưa lưu. Tiếp tục?')) return;
    setBusy(true);
    try {
      const r = await apiFetch(`/api/v1/blueprints/${editing.id}`);
      if (!r.ok) throw new Error();
      edit(await r.json());
    } catch { setError('Không tải được bản mới. Nội dung hiện tại vẫn được giữ lại.'); }
    finally { setBusy(false); }
  }
  async function archive(value: Blueprint) {
    if (!csrfToken) { setError('Chưa lấy được token bảo vệ phiên. Tải lại trang trước khi lưu trữ.'); return; }
    if (busy || !window.confirm(`Lưu trữ ma trận “${value.name}”? Nội dung được giữ lại để xem.`)) return;
    setBusy(true); setError('');
    try {
      const result = await apiFetch(`/api/v1/blueprints/${value.id}/archive`, {
        method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken }, body: JSON.stringify({ version: value.version }),
      });
      if (!result.ok) {
        setError(result.status === 409 ? 'Ma trận đã thay đổi. Tải lại danh sách trước khi lưu trữ.' : 'Không lưu trữ được ma trận.');
        return;
      }
      setNotice('Đã lưu trữ ma trận. Nội dung được giữ lại trong mục Đã lưu trữ.');
      await load();
    } catch { setError('Không kết nối được máy chủ. Hãy tải lại để kiểm tra trạng thái trước khi thử lại.'); }
    finally { setBusy(false); }
  }
  function updateSlot(index: number, patch: Partial<Slot>) {
    if (editing) setEditing({ ...editing, slots: editing.slots.map((s, i) => i === index ? { ...s, ...patch } : s) });
  }
  const subject = catalog.find(s => s.id === editing?.subjectId);
  const field = (name: string) => fields[name] && <span role="alert" className="text-danger">{fields[name].join(' ')}</span>;
  return <div>
    <div className="page-header"><h1 className="page-title">Ma trận đề</h1><p className="page-subtitle">Thiết kế chủ đề, độ khó, số câu và điểm cho đề thi</p></div>
    {error && <div role="alert" className="card" style={{ padding: 16 }}>{error}</div>}
    {notice && <p role="status">{notice}</p>}
    {editing ? <form onSubmit={save} onChange={() => setNotice('')}>
      <fieldset disabled={busy} style={{ border: 0, padding: 0 }}>
        <div className="page-actions"><button type="button" className="btn btn-secondary" onClick={() => { if (window.confirm('Quay lại danh sách? Nội dung chưa lưu sẽ bị bỏ.')) { setEditing(null); void load(); } }}>Danh sách</button>
          <button className="btn btn-primary" disabled={!!editing.archivedAt} type="submit">{busy ? 'Đang lưu…' : 'Lưu ma trận'}</button>
          {conflict && <button className="btn btn-secondary" type="button" onClick={reload}>Tải phiên bản mới</button>}</div>
        {editing.archivedAt && <p role="status">Đã lưu trữ — chỉ xem nội dung.</p>}
        <fieldset disabled={!!editing.archivedAt} style={{ border: 0, padding: 0 }}>
        <div className="card" style={{ padding: 20, display: 'grid', gap: 16 }}>
          <label>Tên ma trận<input className="form-input" required maxLength={200} value={editing.name} onChange={e => setEditing({ ...editing, name: e.target.value })} />{field('name')}</label>
          <label>Môn học<select aria-label="Môn học" className="form-select" required disabled={!!editing.id} value={editing.subjectId} onChange={e => setEditing({ ...editing, subjectId: e.target.value, slots: [] })}>
            <option value="">Chọn môn học</option>{catalog.filter(s => s.canWrite).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>{field('subjectId')}</label>
          <label>Thời gian (phút)<input className="form-input" type="number" min={1} max={1440} required value={editing.durationMinutes} onChange={e => setEditing({ ...editing, durationMinutes: Number(e.target.value) })} />{field('durationMinutes')}</label>
          <label>Tổng số câu yêu cầu<input className="form-input" type="number" min={1} max={1000} required value={editing.totalQuestions} onChange={e => setEditing({ ...editing, totalQuestions: Number(e.target.value) })} />{field('totalQuestions')}</label>
          <label>Tổng điểm yêu cầu<input className="form-input" type="number" min={0.0001} max={10000} step="0.0001" required value={editing.totalPoints} onChange={e => setEditing({ ...editing, totalPoints: Number(e.target.value) })} />{field('totalPoints')}</label>
        </div>
        <div className="table-container" style={{ overflowX: 'auto', marginTop: 16 }}><table><caption>Ma trận chủ đề và độ khó</caption><thead><tr><th>Chủ đề</th><th>Độ khó</th><th>Số câu</th><th>Điểm/câu</th><th>Thao tác</th></tr></thead><tbody>
          {editing.slots.map((s, i) => <tr key={i}>
            <td><select aria-label={`Chủ đề ô ${i + 1}`} required value={s.topicId} onChange={e => updateSlot(i, { topicId: e.target.value })}><option value="">Chọn chủ đề</option>{subject?.topics.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}</select>{field(`slots[${i}].topicId`)}{field(`slots[${i}]`)}</td>
            <td><select aria-label={`Độ khó ô ${i + 1}`} value={s.difficulty} onChange={e => updateSlot(i, { difficulty: e.target.value })}><option value="EASY">Dễ</option><option value="MEDIUM">Trung bình</option><option value="HARD">Khó</option></select>{field(`slots[${i}].difficulty`)}</td>
            <td><input aria-label={`Số câu ô ${i + 1}`} type="number" min={1} max={1000} required value={s.questionCount} onChange={e => updateSlot(i, { questionCount: Number(e.target.value) })} />{field(`slots[${i}].questionCount`)}</td>
            <td><input aria-label={`Điểm mỗi câu ô ${i + 1}`} type="number" min={0.0001} max={10000} step="0.0001" required value={s.pointsPerQuestion} onChange={e => updateSlot(i, { pointsPerQuestion: Number(e.target.value) })} />{field(`slots[${i}].pointsPerQuestion`)}</td>
            <td><button type="button" className="btn btn-secondary" aria-label={`Xóa ô ${i + 1}`} onClick={() => setEditing({ ...editing, slots: editing.slots.filter((_, n) => n !== i) })}>Xóa ô</button></td>
          </tr>)}
        </tbody></table></div>{field('slots')}
        <p aria-live="polite">Tổng các ô: {editing.slots.reduce((n, s) => n + s.questionCount, 0)} câu · {(editing.slots.reduce((n, s) => n + s.questionCount * s.pointsPerQuestion, 0)).toFixed(4)} điểm</p>
        <button type="button" className="btn btn-secondary" disabled={!subject || editing.slots.length >= 200} onClick={() => setEditing({ ...editing, slots: [...editing.slots, { topicId: '', difficulty: 'EASY', questionCount: 1, pointsPerQuestion: 1 }] })}>Thêm ô</button>
        </fieldset>
      </fieldset>
    </form> : <>
      <div className="page-actions"><button className="btn btn-primary" disabled={busy || !catalog.some(s => s.canWrite)} onClick={() => edit(empty())}>Tạo ma trận</button><button className="btn btn-secondary" disabled={busy} onClick={load}>Tải lại</button></div>
      <label><input type="checkbox" checked={archived} disabled={busy} onChange={e => { setBusy(true); setPage(1); setArchived(e.target.checked); }} />Đã lưu trữ</label>
      {busy ? <p role="status">Đang tải…</p> : <div className="table-container"><table><thead><tr><th>Tên</th><th>Số câu</th><th>Điểm</th><th>Thao tác</th></tr></thead><tbody>{items.map(b => <tr key={b.id}><td>{b.name}</td><td>{b.totalQuestions}</td><td>{b.totalPoints}</td><td><button className="btn btn-secondary" onClick={() => edit(b)}>Mở {b.name}</button>{!archived && <button className="btn btn-secondary" disabled={busy} onClick={() => archive(b)}>Lưu trữ {b.name}</button>}</td></tr>)}</tbody></table>{items.length === 0 && <p>Chưa có ma trận trong phạm vi của bạn.</p>}</div>}
      <div className="page-actions"><button className="btn btn-secondary" disabled={busy || page === 1} onClick={() => setPage(page - 1)}>Trang trước</button><span>Trang {page} · {total} ma trận</span><button className="btn btn-secondary" disabled={busy || page * 20 >= total} onClick={() => setPage(page + 1)}>Trang sau</button></div>
    </>}
  </div>;
>>>>>>> origin/dev
}
