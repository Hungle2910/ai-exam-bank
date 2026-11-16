'use client';

import React, { useEffect, useState } from 'react';
import {
  Cpu, ArrowRight, Clock, Lock
} from 'lucide-react';
import clsx from 'clsx';
import { useAuth } from '@/contexts/AuthContext';
import { ROLE_SHORT_LABEL } from '@/components/layout/Sidebar';
import {
  useWorkflowStore,
  computeSlotStats,
  setWorkflowNavIntent,
} from '@/lib/workflowStore';
import { fetchScopedJobs, JobApiItem } from '@/lib/jobClient';
import { formatDateTime } from '@/lib/fetchOrMock';

export default function DashboardPage({ onNavigate }: { onNavigate?: (href: string) => void }) {
  const { user, primaryRole, apiFetch } = useAuth();
  const wf = useWorkflowStore();

  const [schoolsCount, setSchoolsCount] = useState<number | null>(null);
  const [recentJobs, setRecentJobs] = useState<JobApiItem[]>([]);

  const isAdmin = primaryRole === 'MinistryAdmin' || primaryRole === 'SchoolAdmin';
  const isReviewer = primaryRole === 'DepartmentHead';

  useEffect(() => {
    let active = true;
    fetchScopedJobs().then(res => {
      if (!active) return;
      setRecentJobs(res.jobs.slice(0, 5));
    });
    if (isAdmin) {
      apiFetch('/api/v1/admin/schools')
        .then(async res => {
          if (res.ok && active) {
            const list = (await res.json()) as Array<{ id: string }>;
            setSchoolsCount(list.length);
          }
        })
        .catch(() => {
          // ignore
        });
    }
    return () => {
      active = false;
    };
  }, [apiFetch, isAdmin]);

  const draftOrRejected = wf.questions.filter(
    q => q.status === 'DRAFT' || q.status === 'REJECTED',
  );
  const pendingReviewQuestions = wf.questions.filter(
    q => q.status === 'REVIEW_REQUIRED',
  );
  const missingSlots = wf.blueprints.flatMap(bp =>
    bp.slots
      .map(slot => ({
        bp,
        slot,
        stats: computeSlotStats(slot, wf.questions),
      }))
      .filter(item => item.stats.missingCount > 0),
  );

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-title">
            Việc cần xử lý: {user?.userName}
          </h1>
          <p className="page-subtitle">
            Danh mục công việc đang mở trên bàn soạn đề theo vai trò{' '}
            <strong>{primaryRole ? ROLE_SHORT_LABEL[primaryRole] : 'Giáo viên'}</strong>; bấm vào từng dòng để đi thẳng tới hồ sơ tương ứng
          </p>
        </div>
        <div className="page-actions">
          <button
            className="btn btn-secondary"
            onClick={() => onNavigate?.('/questions')}
          >
            Mở Mục lục câu hỏi
          </button>
          <button
            className="btn btn-primary"
            onClick={() => onNavigate?.('/blueprints')}
          >
            Mở Bàn soạn ma trận đề <ArrowRight size={13} />
          </button>
        </div>
      </div>

      {/* Two-Column Editorial Work Ledger */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: '1.25fr 1fr',
          gap: 20,
          alignItems: 'start',
        }}
        className="desk-ledger-grid"
      >
        {/* Left Column: Primary Authoring & Review Lists */}
        <div style={{ display: 'grid', gap: 18 }}>
          {/* Section I: Tiếp tục soạn đề & Bổ sung ô ma trận thiếu */}
          <section className="card">
            <div className="card-header">
              <div>
                <div className="card-title">I. Tiếp tục soạn đề ({missingSlots.length} ô ma trận còn thiếu câu)</div>
                <div className="card-subtitle">
                  Các ô trong ma trận đề chưa đủ số lượng câu hỏi đã duyệt
                </div>
              </div>
              <button
                className="btn btn-ghost btn-sm"
                onClick={() => onNavigate?.('/blueprints')}
              >
                Mở bảng ma trận <ArrowRight size={12} />
              </button>
            </div>
            <div>
              {missingSlots.map(({ bp, slot, stats }) => (
                <div
                  key={slot.id}
                  style={{
                    padding: '12px 16px',
                    borderBottom: '1px solid var(--rule-light)',
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    gap: 12,
                    flexWrap: 'wrap',
                  }}
                >
                  <div>
                    <div style={{ fontFamily: 'var(--font-serif)', fontWeight: 700, fontSize: 15, color: 'var(--ink-900)' }}>
                      {bp.name} · Ô: {slot.topic} ({slot.difficulty})
                    </div>
                    <div style={{ fontSize: 12.5, color: 'var(--ink-600)', marginTop: 2 }}>
                      Thực tế đã duyệt:{' '}
                      <strong className="mono-num">
                        {stats.approvedCount}/{slot.questionCount} câu
                      </strong>{' '}
                      ({slot.pointsPerQuestion}đ/câu) ·{' '}
                      <span className="badge badge-red" style={{ marginLeft: 4 }}>
                        Thiếu {stats.missingCount} câu
                      </span>
                    </div>
                  </div>
                  <div style={{ display: 'flex', gap: 6 }}>
                    <button
                      className="btn btn-secondary btn-sm"
                      onClick={() => {
                        setWorkflowNavIntent({
                          blueprintId: bp.id,
                          slotIds: [slot.id],
                          subject: slot.subject,
                          topic: slot.topic,
                          difficulty: slot.difficulty,
                        });
                        onNavigate?.('/questions');
                      }}
                    >
                      Tìm câu phù hợp
                    </button>
                    {(primaryRole === 'Teacher' || primaryRole === 'DepartmentHead') && (
                      <button
                        className="btn btn-primary btn-sm"
                        onClick={() => {
                          setWorkflowNavIntent({
                            blueprintId: bp.id,
                            slotIds: [slot.id],
                            subject: slot.subject,
                            topic: slot.topic,
                            difficulty: slot.difficulty,
                          });
                          onNavigate?.('/generate');
                        }}
                      >
                        <Cpu size={13} />
                        Gợi ý AI
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </section>

          {/* Section II: Câu hỏi chờ duyệt / Cần chỉnh sửa */}
          <section className="card">
            <div className="card-header">
              <div>
                <div className="card-title">
                  {isReviewer
                    ? `II. Câu hỏi chờ duyệt (${pendingReviewQuestions.length} hồ sơ)`
                    : `II. Câu hỏi chờ duyệt & Bản thảo cần hiệu đính (${
                        draftOrRejected.length + pendingReviewQuestions.length
                      })`}
                </div>
                <div className="card-subtitle">
                  {isReviewer
                    ? 'Hồ sơ câu hỏi đã gửi vào bàn thẩm định của tổ bộ môn'
                    : 'Các câu hỏi bị trả về kèm bút phê đỏ cần chỉnh sửa thành phiên bản mới'}
                </div>
              </div>
              <button
                className="btn btn-ghost btn-sm"
                onClick={() => onNavigate?.(isReviewer || isAdmin ? '/review' : '/questions')}
              >
                {isReviewer ? 'Mở Bàn duyệt' : 'Mở Mục lục'} <ArrowRight size={12} />
              </button>
            </div>
            <div>
              {[...draftOrRejected, ...pendingReviewQuestions].slice(0, 5).map(q => (
                <div
                  key={q.id}
                  style={{
                    padding: '12px 16px',
                    borderBottom: '1px solid var(--rule-light)',
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'flex-start',
                    gap: 12,
                  }}
                >
                  <div style={{ minWidth: 0, flex: 1 }}>
                    <div style={{ display: 'flex', gap: 6, alignItems: 'center', marginBottom: 4, flexWrap: 'wrap' }}>
                      <span
                        className={clsx(
                          'badge',
                          q.status === 'REJECTED'
                            ? 'badge-red'
                            : q.status === 'REVIEW_REQUIRED'
                            ? 'badge-amber'
                            : 'badge-gray',
                        )}
                      >
                        {q.status === 'REJECTED'
                          ? 'Trả về yêu cầu sửa'
                          : q.status === 'REVIEW_REQUIRED'
                          ? 'Chờ duyệt'
                          : 'Bản nháp'}
                      </span>
                      <code style={{ fontSize: 11.5, color: 'var(--ink-600)' }}>
                        {q.id} · Phiên bản {q.revision} (v{q.version})
                      </code>
                      <span style={{ fontSize: 12, color: 'var(--ink-500)' }}>
                        {q.subject} / {q.topic}
                      </span>
                    </div>
                    <div style={{ fontFamily: 'var(--font-serif)', fontSize: 14.5, fontWeight: 600, color: 'var(--ink-900)' }}>
                      {q.content}
                    </div>
                    {q.rejectReason && (
                      <div className="margin-note red-ink" style={{ marginTop: 6, padding: '5px 9px' }}>
                        <strong>Bút phê trả về:</strong> {q.rejectReason}
                      </div>
                    )}
                  </div>
                  <button
                    className="btn btn-secondary btn-sm"
                    style={{ flexShrink: 0 }}
                    onClick={() =>
                      onNavigate?.(
                        q.status === 'REVIEW_REQUIRED' && (isReviewer || isAdmin)
                          ? '/review'
                          : '/questions',
                      )
                    }
                  >
                    Mở xử lý <ArrowRight size={12} />
                  </button>
                </div>
              ))}
            </div>
          </section>
        </div>

        {/* Right Column: Exam Papers, Background Tasks & Audit */}
        <div style={{ display: 'grid', gap: 18 }}>
          {/* Section III: Đề thi gần đây & Xét chọn */}
          <section className="card">
            <div className="card-header">
              <div>
                <div className="card-title">III. Hồ sơ đề thi &amp; Bản chốt A4</div>
                <div className="card-subtitle">
                  Đề đã lắp ráp theo ma trận và các phiên bản dự tuyển kỳ thi
                </div>
              </div>
              <button className="btn btn-ghost btn-sm" onClick={() => onNavigate?.('/exams')}>
                Xem bản in A4 <ArrowRight size={12} />
              </button>
            </div>
            <div>
              {wf.exams.map(ex => (
                <div
                  key={ex.id}
                  style={{
                    padding: '11px 16px',
                    borderBottom: '1px solid var(--rule-light)',
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    gap: 10,
                  }}
                >
                  <div>
                    <div style={{ fontFamily: 'var(--font-serif)', fontWeight: 700, fontSize: 14.5 }}>
                      {ex.title}
                    </div>
                    <div style={{ fontSize: 12, color: 'var(--ink-600)' }}>
                      <span className="mono-num">
                        {ex.questions.length} câu · {ex.totalPoints.toFixed(2)}đ · {ex.durationMinutes} phút
                      </span>{' '}
                      · Tác giả: <code>{ex.author}</code>
                    </div>
                  </div>
                  <button
                    className="btn btn-secondary btn-sm"
                    onClick={() => onNavigate?.('/exams')}
                  >
                    {ex.status === 'FINALIZED' ? (
                      <>
                        <Lock size={11} /> Bản chốt
                      </>
                    ) : (
                      'Rà soát'
                    )}
                  </button>
                </div>
              ))}
            </div>
          </section>

          {/* Section IV: Tác vụ cần xử lý & Nhật ký gần đây */}
          <section className="card">
            <div className="card-header">
              <div>
                <div className="card-title">IV. Tác vụ cần xử lý &amp; Nhật ký thẩm định</div>
                <div className="card-subtitle">
                  {schoolsCount !== null
                    ? `Đã kết nối ${schoolsCount} đơn vị trường từ máy chủ`
                    : 'Theo dõi tiến trình tạo câu hỏi AI, nhập CSV và lịch sử quyết định'}
                </div>
              </div>
              <button className="btn btn-ghost btn-sm" onClick={() => onNavigate?.('/ops')}>
                Mở tác vụ <ArrowRight size={12} />
              </button>
            </div>
            <div>
              {recentJobs.slice(0, 2).map(j => (
                <div
                  key={j.jobId}
                  style={{
                    padding: '9px 16px',
                    borderBottom: '1px solid var(--rule-light)',
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    fontSize: 12.5,
                  }}
                >
                  <div>
                    <strong>Tác vụ {j.jobType}</strong> · <code>{j.jobId.slice(0, 10)}</code>
                    <div style={{ color: 'var(--ink-500)', fontSize: 11.5 }}>
                      {j.safeMessage ?? `Tiến độ: ${j.progressPercentage ?? 0}%`}
                    </div>
                  </div>
                  <span
                    className={clsx(
                      'badge',
                      j.status === 'Completed'
                        ? 'badge-green'
                        : j.status === 'Failed'
                        ? 'badge-red'
                        : 'badge-amber',
                    )}
                  >
                    {j.status}
                  </span>
                </div>
              ))}

              {wf.auditTrail.slice(0, 4).map(item => (
                <div
                  key={item.id}
                  style={{
                    padding: '9px 16px',
                    borderBottom: '1px solid var(--rule-light)',
                    fontSize: 12.5,
                  }}
                >
                  <div style={{ fontWeight: 600, color: 'var(--ink-900)' }}>{item.detail}</div>
                  <div style={{ color: 'var(--ink-500)', fontSize: 11.5, marginTop: 2, display: 'flex', alignItems: 'center', gap: 6 }}>
                    <code>{item.actor}</code>
                    <span>·</span>
                    <code>{item.target}</code>
                    <span>·</span>
                    <Clock size={10} />
                    <span className="mono-num">{formatDateTime(item.occurredAt)}</span>
                  </div>
                </div>
              ))}
            </div>
          </section>
        </div>
      </div>

      <style>{`
        @media (max-width: 960px) {
          .desk-ledger-grid { grid-template-columns: 1fr !important; }
        }
      `}</style>
    </div>
  );
}
