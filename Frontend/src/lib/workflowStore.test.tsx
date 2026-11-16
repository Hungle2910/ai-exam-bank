import { describe, expect, it } from 'vitest';
import { validateAiDraftSafety } from './jobClient';
import {
  getWorkflowState,
  upsertBankQuestion,
  decideQuestionReviewInStore,
  computeSlotStats,
  assembleExamFromBlueprint,
  finalizeExamInStore,
  submitExamToEventInStore,
  decideExamEventSelectionInStore,
} from './workflowStore';

describe('M1–M5 Academic Workflow & Security Rules', () => {
  it('enforces M3-04 AiDraftSafetyGate invariants (m3-ai-draft-v1)', () => {
    const valid = validateAiDraftSafety({
      stem: 'Đạo hàm của y = x² là?',
      options: ['2x', 'x', '2', 'x²'],
      correctIndex: 0,
      explanation: 'Áp dụng (x^n)′ = n·x^(n−1)',
    });
    expect(valid.succeeded).toBe(true);
    expect(valid.policyVersion).toBe('m3-ai-draft-v1');
    expect(valid.action).toBe('REVIEW_REQUIRED');

    const fakeApproved = validateAiDraftSafety({
      stem: 'Câu hỏi giả mạo trạng thái',
      options: ['A1', 'B1', 'C1', 'D1'],
      correctIndex: 0,
      rawStatus: 'APPROVED',
    });
    expect(fakeApproved.succeeded).toBe(false);
    expect(fakeApproved.action).toBe('BLOCK');

    const duplicateOpts = validateAiDraftSafety({
      stem: 'Câu hỏi trùng phương án',
      options: ['2x', '2x', '2', 'x²'],
      correctIndex: 0,
    });
    expect(duplicateOpts.succeeded).toBe(false);
  });

  it('enforces self-review ban, reject reason, and optimistic concurrency on question review', () => {
    const created = upsertBankQuestion({
      content: 'Câu hỏi kiểm thử luồng duyệt M3-03',
      options: ['Phương án A', 'Phương án B', 'Phương án C', 'Phương án D'],
      correctIndex: 1,
      subject: 'Toán',
      topic: 'Đạo hàm',
      difficulty: 'HARD',
      points: 0.5,
      source: 'AI_DRAFT',
      actorUserName: 'gv.test.author',
      submitForReview: true,
    });

    // 1. Self-review must be blocked
    const selfAttempt = decideQuestionReviewInStore({
      questionId: created.id,
      revision: created.revision,
      expectedVersion: created.version,
      reviewerUserName: 'gv.test.author',
      approve: true,
    });
    expect(selfAttempt.ok).toBe(false);
    expect(selfAttempt.code).toBe('SELF_REVIEW');

    // 2. Reject without reason must be blocked
    const emptyReject = decideQuestionReviewInStore({
      questionId: created.id,
      revision: created.revision,
      expectedVersion: created.version,
      reviewerUserName: 'tbm.reviewer',
      approve: false,
      reason: '   ',
    });
    expect(emptyReject.ok).toBe(false);
    expect(emptyReject.code).toBe('REASON_REQUIRED');

    // 3. Stale expectedVersion must be blocked with CONFLICT
    const staleAttempt = decideQuestionReviewInStore({
      questionId: created.id,
      revision: created.revision,
      expectedVersion: created.version - 1,
      reviewerUserName: 'tbm.reviewer',
      approve: true,
    });
    expect(staleAttempt.ok).toBe(false);
    expect(staleAttempt.code).toBe('CONFLICT');

    // 4. Valid independent reviewer approval succeeds
    const validApprove = decideQuestionReviewInStore({
      questionId: created.id,
      revision: created.revision,
      expectedVersion: created.version,
      reviewerUserName: 'tbm.reviewer',
      approve: true,
    });
    expect(validApprove.ok).toBe(true);
  });

  it('creates a new immutable revision when editing an APPROVED question', () => {
    const approvedQ = getWorkflowState().questions.find(q => q.status === 'APPROVED')!;
    const prevRevision = approvedQ.revision;

    const nextQ = upsertBankQuestion({
      id: approvedQ.id,
      content: `${approvedQ.content} (Bản chỉnh sửa)`,
      options: approvedQ.options,
      correctIndex: approvedQ.correctIndex,
      subject: approvedQ.subject,
      topic: approvedQ.topic,
      difficulty: approvedQ.difficulty,
      points: approvedQ.points,
      actorUserName: approvedQ.ownerUserName,
      submitForReview: false,
    });

    expect(nextQ.revision).toBe(prevRevision + 1);
    expect(nextQ.status).toBe('DRAFT');
    expect(nextQ.revisions.length).toBeGreaterThanOrEqual(2);
    expect(nextQ.revisions[1].status).toBe('APPROVED');
  });

  it('assembles exams from APPROVED revisions only and enforces Ministry two-person finalization rule', () => {
    const bp = getWorkflowState().blueprints.find(b => b.id === 'bp-2')!;
    const slotStats = computeSlotStats(bp.slots[0], getWorkflowState().questions);
    expect(slotStats.approvedCount).toBeGreaterThan(0);

    const { exam } = assembleExamFromBlueprint(bp, 'ministry.author', 'MINISTRY');
    expect(exam.questions.every(q => q.status === 'APPROVED')).toBe(true);

    // Same MinistryAdmin cannot self-confirm
    const selfConfirm = finalizeExamInStore(exam.id, 'ministry.author');
    expect(selfConfirm.ok).toBe(false);

    // Different MinistryAdmin confirms when valid
    if (exam.issues.length === 0) {
      const otherConfirm = finalizeExamInStore(exam.id, 'ministry.confirmer');
      expect(otherConfirm.ok).toBe(true);
    }
  });

  it('transitions competing ExamEvent submissions to NOT_SELECTED and supersedes pending revisions', () => {
    const stateBefore = getWorkflowState();
    const validExam = stateBefore.exams.find(e => e.issues.length === 0)!;
    const eventId = stateBefore.examEvents[0].id;

    // Teacher submits a new revision -> previous PENDING_REVIEW of same teacher becomes SUPERSEDED
    const subRes = submitExamToEventInStore({
      exam: validExam,
      eventId,
      teacherUserName: 'gv.nguyen.van.a',
    });
    expect(subRes.ok).toBe(true);

    const afterSubmit = getWorkflowState().examSubmissions.filter(
      s => s.eventId === eventId && s.teacherUserName === 'gv.nguyen.van.a',
    );
    expect(afterSubmit.some(s => s.status === 'SUPERSEDED')).toBe(true);

    const latestPending = afterSubmit.find(s => s.status === 'PENDING_REVIEW')!;
    const selectRes = decideExamEventSelectionInStore({
      submissionId: latestPending.id,
      expectedVersion: latestPending.expectedVersion,
      reviewerUserName: 'tbm.toan.thpt.a',
      decision: 'SELECT',
    });
    expect(selectRes.ok).toBe(true);

    const eventAfter = getWorkflowState().examEvents.find(e => e.id === eventId)!;
    expect(eventAfter.selectedSubmissionId).toBe(latestPending.id);
    const otherSub = getWorkflowState().examSubmissions.find(s => s.id === 'sub-102')!;
    expect(otherSub.status).toBe('NOT_SELECTED');
  });
});
