/**
 * Shared Academic Workflow Store (M1–M5).
 * Connects Question Bank, CSV Import, Blueprints, AI Generation, Review Queue,
 * Exam Assembly/Finalization, Exam Event Selection, and Audit Log across pages
 * so end-to-end user journeys work seamlessly.
 */

import { useSyncExternalStore } from 'react';

export type Difficulty = 'EASY' | 'MEDIUM' | 'HARD';
export type QuestionStatus = 'DRAFT' | 'REVIEW_REQUIRED' | 'APPROVED' | 'REJECTED';
export type QuestionSource = 'MANUAL' | 'IMPORT' | 'AI_DRAFT' | 'SEED';

export interface QuestionCitation {
  sourceId: string;
  source: string;
  locator: string;
  excerpt: string;
}

export interface QuestionRevisionRecord {
  revision: number;
  version: number;
  content: string;
  options: string[];
  correctIndex: number;
  status: QuestionStatus;
  author: string;
  updatedAt: string;
  note?: string;
}

export interface ReviewDecisionRecord {
  id: string;
  revision: number;
  actor: string;
  action: 'SUBMIT' | 'APPROVE' | 'REJECT';
  reason?: string;
  occurredAt: string;
}

export interface BankQuestion {
  id: string;
  revision: number;
  version: number;
  content: string;
  options: string[];
  correctIndex: number;
  explanation?: string;
  subject: string;
  topic: string;
  difficulty: Difficulty;
  points: number;
  status: QuestionStatus;
  source: QuestionSource;
  ownerUserName: string;
  author: string;
  updatedAt: string;
  submittedAt?: string;
  rejectReason?: string;
  citation?: QuestionCitation;
  revisions: QuestionRevisionRecord[];
  decisions: ReviewDecisionRecord[];
}

export interface KnowledgeDocument {
  sourceId: string;
  title: string;
  subject: string;
  topic: string;
  scopeLabel: string;
  chunkCount: number;
  updatedAt: string;
  status: 'READY' | 'INDEXING';
}

export interface BlueprintSlotItem {
  id: string;
  subject: string;
  topic: string;
  difficulty: Difficulty;
  questionCount: number;
  pointsPerQuestion: number;
}

export interface BlueprintItem {
  id: string;
  name: string;
  subject: string;
  grade: string;
  totalPoints: number;
  durationMinutes: number;
  author: string;
  updatedAt: string;
  slots: BlueprintSlotItem[];
}

export type ExamAssemblyStatus = 'ASSEMBLING' | 'VALIDATED' | 'FINALIZED';

export interface PinnedExamQuestion {
  id: string;
  revision: number;
  version: number;
  content: string;
  options: string[];
  correctIndex: number;
  subject: string;
  topic: string;
  difficulty: Difficulty;
  points: number;
  status: QuestionStatus;
}

export interface AssembledExam {
  id: string;
  title: string;
  blueprintId: string;
  blueprintName: string;
  subject: string;
  scopeLevel: 'SCHOOL' | 'MINISTRY';
  status: ExamAssemblyStatus;
  durationMinutes: number;
  totalPoints: number;
  author: string;
  confirmedBy?: string;
  finalizedAt?: string;
  updatedAt: string;
  questions: PinnedExamQuestion[];
  issues: string[];
}

export type ExamCandidateStatus =
  | 'DRAFT'
  | 'PENDING_REVIEW'
  | 'APPROVED'
  | 'NOT_SELECTED'
  | 'REJECTED'
  | 'SUPERSEDED';

export interface ExamCandidateSubmission {
  id: string;
  eventId: string;
  examTitle: string;
  revision: number;
  expectedVersion: number;
  teacherUserName: string;
  blueprintName: string;
  questionCount: number;
  totalPoints: number;
  allQuestionsApproved: boolean;
  status: ExamCandidateStatus;
  submittedAt: string;
  decidedBy?: string;
  decidedAt?: string;
  rejectReason?: string;
  questions: PinnedExamQuestion[];
}

export interface ExamSelectionEvent {
  id: string;
  name: string;
  schoolName: string;
  departmentName: string;
  subject: string;
  grade: string;
  termLabel: string;
  selectedSubmissionId: string | null;
  updatedAt: string;
  history: Array<{
    id: string;
    occurredAt: string;
    actor: string;
    action: 'SUBMITTED' | 'SELECTED' | 'REJECTED' | 'SUPERSEDED';
    submissionId: string;
    revision: number;
    detail: string;
  }>;
}

export interface LocalAuditItem {
  id: string;
  occurredAt: string;
  actor: string;
  action:
    | 'ROLE_GRANTED'
    | 'ROLE_REVOKED'
    | 'QUESTION_APPROVED'
    | 'QUESTION_REJECTED'
    | 'LOGIN'
    | 'QUESTION_CREATED'
    | 'QUESTION_EDITED'
    | 'IMPORT_COMMITTED'
    | 'REVIEW_SUBMITTED'
    | 'DRAFT_GENERATED'
    | 'EXAM_FINALIZED'
    | 'EXAM_SELECTED'
    | 'EXAM_RETURNED';
  target: string;
  detail: string;
  schoolName: string;
  correlationId?: string;
}

export interface WorkflowNavigationIntent {
  blueprintId?: string;
  slotIds?: string[];
  subject?: string;
  topic?: string;
  difficulty?: Difficulty;
}

export interface WorkflowState {
  questions: BankQuestion[];
  knowledgeDocs: KnowledgeDocument[];
  blueprints: BlueprintItem[];
  exams: AssembledExam[];
  examEvents: ExamSelectionEvent[];
  examSubmissions: ExamCandidateSubmission[];
  auditTrail: LocalAuditItem[];
  navIntent: WorkflowNavigationIntent | null;
}

const INITIAL_QUESTIONS: BankQuestion[] = [
  {
    id: 'q-1001',
    revision: 2,
    version: 3,
    content: 'Đạo hàm của hàm số y = x³ − 3x + 2 trên tập số thực ℝ là?',
    options: ['y′ = 3x² − 3', 'y′ = 3x² + 3', 'y′ = x² − 3', 'y′ = 3x − 3'],
    correctIndex: 0,
    explanation: 'Áp dụng quy tắc đạo hàm hàm đa thức: (x³)′ = 3x², (−3x)′ = −3, (2)′ = 0.',
    subject: 'Toán',
    topic: 'Đạo hàm',
    difficulty: 'EASY',
    points: 0.25,
    status: 'REVIEW_REQUIRED',
    source: 'MANUAL',
    ownerUserName: 'gv.nguyen.van.a',
    author: 'gv.nguyen.van.a',
    updatedAt: '2026-10-16T08:12:00Z',
    submittedAt: '2026-10-16T08:12:00Z',
    revisions: [
      {
        revision: 2,
        version: 3,
        content: 'Đạo hàm của hàm số y = x³ − 3x + 2 trên tập số thực ℝ là?',
        options: ['y′ = 3x² − 3', 'y′ = 3x² + 3', 'y′ = x² − 3', 'y′ = 3x − 3'],
        correctIndex: 0,
        status: 'REVIEW_REQUIRED',
        author: 'gv.nguyen.van.a',
        updatedAt: '2026-10-16T08:12:00Z',
        note: 'Bổ sung miền xác định ℝ rõ ràng theo góp ý bộ môn',
      },
      {
        revision: 1,
        version: 2,
        content: 'Đạo hàm của y = x³ − 3x + 2 là?',
        options: ['3x² − 3', '3x² + 3', 'x² − 3', '3x − 3'],
        correctIndex: 0,
        status: 'REJECTED',
        author: 'gv.nguyen.van.a',
        updatedAt: '2026-10-15T09:00:00Z',
        note: 'Bản nháp đầu tiên',
      },
    ],
    decisions: [
      {
        id: 'dec-101',
        revision: 1,
        actor: 'tbm.toan.thpt.a',
        action: 'REJECT',
        reason: 'Cần ghi rõ ký hiệu y′ và tập xác định của hàm số.',
        occurredAt: '2026-10-15T14:20:00Z',
      },
      {
        id: 'dec-102',
        revision: 2,
        actor: 'gv.nguyen.van.a',
        action: 'SUBMIT',
        occurredAt: '2026-10-16T08:12:00Z',
      },
    ],
  },
  {
    id: 'q-1002',
    revision: 1,
    version: 2,
    content: 'Cho hàm số y = x³ − 3x² + 2. Giá trị cực đại của hàm số bằng bao nhiêu?',
    options: ['2', '−2', '0', '1'],
    correctIndex: 0,
    explanation: 'y′ = 3x² − 6x = 0 ⇔ x = 0 hoặc x = 2. Hàm số đạt cực đại tại x = 0 với y(0) = 2.',
    subject: 'Toán',
    topic: 'Đạo hàm',
    difficulty: 'EASY',
    points: 0.25,
    status: 'APPROVED',
    source: 'MANUAL',
    ownerUserName: 'gv.nguyen.van.a',
    author: 'gv.nguyen.van.a',
    updatedAt: '2026-10-15T10:00:00Z',
    submittedAt: '2026-10-15T08:30:00Z',
    revisions: [
      {
        revision: 1,
        version: 2,
        content: 'Cho hàm số y = x³ − 3x² + 2. Giá trị cực đại của hàm số bằng bao nhiêu?',
        options: ['2', '−2', '0', '1'],
        correctIndex: 0,
        status: 'APPROVED',
        author: 'gv.nguyen.van.a',
        updatedAt: '2026-10-15T10:00:00Z',
      },
    ],
    decisions: [
      {
        id: 'dec-103',
        revision: 1,
        actor: 'tbm.toan.thpt.a',
        action: 'APPROVE',
        occurredAt: '2026-10-15T10:00:00Z',
      },
    ],
  },
  {
    id: 'q-1003',
    revision: 1,
    version: 2,
    content: 'Tiếp tuyến của đồ thị hàm số y = x³ − 2x tại điểm có hoành độ x₀ = 1 có hệ số góc bằng?',
    options: ['1', '−1', '3', '2'],
    correctIndex: 0,
    explanation: 'y′(x) = 3x² − 2 ⇒ y′(1) = 3(1)² − 2 = 1.',
    subject: 'Toán',
    topic: 'Đạo hàm',
    difficulty: 'EASY',
    points: 0.25,
    status: 'APPROVED',
    source: 'IMPORT',
    ownerUserName: 'gv.tran.thi.b',
    author: 'gv.tran.thi.b',
    updatedAt: '2026-10-15T11:15:00Z',
    revisions: [
      {
        revision: 1,
        version: 2,
        content: 'Tiếp tuyến của đồ thị hàm số y = x³ − 2x tại điểm có hoành độ x₀ = 1 có hệ số góc bằng?',
        options: ['1', '−1', '3', '2'],
        correctIndex: 0,
        status: 'APPROVED',
        author: 'gv.tran.thi.b',
        updatedAt: '2026-10-15T11:15:00Z',
      },
    ],
    decisions: [
      {
        id: 'dec-104',
        revision: 1,
        actor: 'tbm.toan.thpt.a',
        action: 'APPROVE',
        occurredAt: '2026-10-15T11:15:00Z',
      },
    ],
  },
  {
    id: 'q-1004',
    revision: 1,
    version: 2,
    content: 'Hàm số y = −x⁴ + 2x² + 3 đồng biến trên khoảng nào dưới đây?',
    options: ['(−∞; −1) và (0; 1)', '(−1; 0) và (1; +∞)', '(−1; 1)', '(0; +∞)'],
    correctIndex: 0,
    explanation: 'y′ = −4x³ + 4x = −4x(x² − 1) > 0 ⇔ x ∈ (−∞; −1) ∪ (0; 1).',
    subject: 'Toán',
    topic: 'Đạo hàm',
    difficulty: 'MEDIUM',
    points: 0.25,
    status: 'APPROVED',
    source: 'AI_DRAFT',
    ownerUserName: 'gv.nguyen.van.a',
    author: 'gv.nguyen.van.a',
    updatedAt: '2026-10-15T15:30:00Z',
    citation: {
      sourceId: 'kb-math12-ch1',
      source: 'SGK Giải tích 12 — Chương I: Ứng dụng đạo hàm',
      locator: 'Mục 1.2, tr. 9',
      excerpt: 'Định lý tính đơn điệu: Nếu f′(x) > 0 trên khoảng K thì hàm số đồng biến trên K.',
    },
    revisions: [
      {
        revision: 1,
        version: 2,
        content: 'Hàm số y = −x⁴ + 2x² + 3 đồng biến trên khoảng nào dưới đây?',
        options: ['(−∞; −1) và (0; 1)', '(−1; 0) và (1; +∞)', '(−1; 1)', '(0; +∞)'],
        correctIndex: 0,
        status: 'APPROVED',
        author: 'gv.nguyen.van.a',
        updatedAt: '2026-10-15T15:30:00Z',
      },
    ],
    decisions: [
      {
        id: 'dec-105',
        revision: 1,
        actor: 'tbm.toan.thpt.a',
        action: 'APPROVE',
        occurredAt: '2026-10-15T15:30:00Z',
      },
    ],
  },
  {
    id: 'q-1005',
    revision: 1,
    version: 2,
    content: 'Số nghiệm thực phân biệt của phương trình log₂(x² − 1) = 3 là?',
    options: ['2', '1', '0', '3'],
    correctIndex: 0,
    explanation: 'Điều kiện x² − 1 > 0. Phương trình ⇔ x² − 1 = 2³ = 8 ⇔ x² = 9 ⇔ x = ±3 (2 nghiệm).',
    subject: 'Toán',
    topic: 'Logarit',
    difficulty: 'MEDIUM',
    points: 0.25,
    status: 'APPROVED',
    source: 'MANUAL',
    ownerUserName: 'gv.nguyen.van.a',
    author: 'gv.nguyen.van.a',
    updatedAt: '2026-10-15T10:22:00Z',
    revisions: [
      {
        revision: 1,
        version: 2,
        content: 'Số nghiệm thực phân biệt của phương trình log₂(x² − 1) = 3 là?',
        options: ['2', '1', '0', '3'],
        correctIndex: 0,
        status: 'APPROVED',
        author: 'gv.nguyen.van.a',
        updatedAt: '2026-10-15T10:22:00Z',
      },
    ],
    decisions: [
      {
        id: 'dec-106',
        revision: 1,
        actor: 'tbm.toan.thpt.a',
        action: 'APPROVE',
        occurredAt: '2026-10-15T10:22:00Z',
      },
    ],
  },
  {
    id: 'q-1006',
    revision: 1,
    version: 2,
    content: 'Tập nghiệm của bất phương trình log₀.₅(x − 1) > −2 là?',
    options: ['(1; 5)', '(−∞; 5)', '(5; +∞)', '[1; 5)'],
    correctIndex: 0,
    explanation: 'Cơ số 0,5 ∈ (0; 1) nên log₀.₅(x − 1) > −2 ⇔ 0 < x − 1 < (0,5)⁻² = 4 ⇔ 1 < x < 5.',
    subject: 'Toán',
    topic: 'Logarit',
    difficulty: 'MEDIUM',
    points: 0.25,
    status: 'APPROVED',
    source: 'IMPORT',
    ownerUserName: 'gv.tran.thi.b',
    author: 'gv.tran.thi.b',
    updatedAt: '2026-10-15T12:10:00Z',
    revisions: [
      {
        revision: 1,
        version: 2,
        content: 'Tập nghiệm của bất phương trình log₀.₅(x − 1) > −2 là?',
        options: ['(1; 5)', '(−∞; 5)', '(5; +∞)', '[1; 5)'],
        correctIndex: 0,
        status: 'APPROVED',
        author: 'gv.tran.thi.b',
        updatedAt: '2026-10-15T12:10:00Z',
      },
    ],
    decisions: [
      {
        id: 'dec-107',
        revision: 1,
        actor: 'tbm.toan.thpt.a',
        action: 'APPROVE',
        occurredAt: '2026-10-15T12:10:00Z',
      },
    ],
  },
  {
    id: 'q-1007',
    revision: 1,
    version: 2,
    content: 'Tìm tất cả các giá trị thực của tham số m để phương trình log₃²x − (m + 2)log₃x + 3m − 1 = 0 có hai nghiệm x₁, x₂ thỏa mãn x₁·x₂ = 27.',
    options: ['m = 1', 'm = 2', 'm = 3', 'm = −1'],
    correctIndex: 0,
    explanation: 'Đặt t = log₃x. Để x₁·x₂ = 27 ⇔ t₁ + t₂ = log₃(27) = 3 ⇒ m + 2 = 3 ⇔ m = 1 (thỏa Δ > 0).',
    subject: 'Toán',
    topic: 'Logarit',
    difficulty: 'HARD',
    points: 0.5,
    status: 'REVIEW_REQUIRED',
    source: 'AI_DRAFT',
    ownerUserName: 'gv.tran.thi.b',
    author: 'gv.tran.thi.b',
    updatedAt: '2026-10-16T09:40:00Z',
    submittedAt: '2026-10-16T09:40:00Z',
    citation: {
      sourceId: 'kb-math12-ch2',
      source: 'SGK Giải tích 12 — Chương II: Hàm số lũy thừa, mũ và logarit',
      locator: 'Bài 5: Phương trình mũ và phương trình logarit, tr. 84',
      excerpt: 'Phương pháp đặt ẩn phụ t = logₐx: tích hai nghiệm x₁x₂ = a^(t₁ + t₂).',
    },
    revisions: [
      {
        revision: 1,
        version: 2,
        content: 'Tìm tất cả các giá trị thực của tham số m để phương trình log₃²x − (m + 2)log₃x + 3m − 1 = 0 có hai nghiệm x₁, x₂ thỏa mãn x₁·x₂ = 27.',
        options: ['m = 1', 'm = 2', 'm = 3', 'm = −1'],
        correctIndex: 0,
        status: 'REVIEW_REQUIRED',
        author: 'gv.tran.thi.b',
        updatedAt: '2026-10-16T09:40:00Z',
      },
    ],
    decisions: [
      {
        id: 'dec-108',
        revision: 1,
        actor: 'gv.tran.thi.b',
        action: 'SUBMIT',
        occurredAt: '2026-10-16T09:40:00Z',
      },
    ],
  },
  {
    id: 'q-1008',
    revision: 1,
    version: 1,
    content: 'Giá trị nhỏ nhất của hàm số y = x + 4/x trên khoảng (0; +∞) bằng?',
    options: ['4', '2', '8', '1'],
    correctIndex: 0,
    explanation: 'Theo bất đẳng thức Cauchy cho x > 0: x + 4/x ≥ 2√(x·4/x) = 4, dấu bằng tại x = 2.',
    subject: 'Toán',
    topic: 'Đạo hàm',
    difficulty: 'MEDIUM',
    points: 0.25,
    status: 'DRAFT',
    source: 'MANUAL',
    ownerUserName: 'gv.nguyen.van.a',
    author: 'gv.nguyen.van.a',
    updatedAt: '2026-10-17T01:10:00Z',
    revisions: [
      {
        revision: 1,
        version: 1,
        content: 'Giá trị nhỏ nhất của hàm số y = x + 4/x trên khoảng (0; +∞) bằng?',
        options: ['4', '2', '8', '1'],
        correctIndex: 0,
        status: 'DRAFT',
        author: 'gv.nguyen.van.a',
        updatedAt: '2026-10-17T01:10:00Z',
      },
    ],
    decisions: [],
  },
  {
    id: 'q-1009',
    revision: 1,
    version: 2,
    content: 'Nguyên tố nào sau đây có độ âm điện lớn nhất trong bảng tuần hoàn?',
    options: ['Flo (F)', 'Oxi (O)', 'Clo (Cl)', 'Nitơ (N)'],
    correctIndex: 0,
    explanation: 'Theo thang Pauling, Flo (F) có độ âm điện lớn nhất là 3,98.',
    subject: 'Hóa học',
    topic: 'Bảng tuần hoàn',
    difficulty: 'EASY',
    points: 0.25,
    status: 'REJECTED',
    source: 'AI_DRAFT',
    ownerUserName: 'gv.nguyen.van.a',
    author: 'gv.nguyen.van.a',
    updatedAt: '2026-10-14T16:48:00Z',
    rejectReason: 'Cần bổ sung ký hiệu nguyên tố kèm tên tiếng Việt chuẩn chương trình GDPT 2018 (Fluorine).',
    citation: {
      sourceId: 'kb-chem10-ch2',
      source: 'SGK Hóa học 10 — Bảng tuần hoàn các nguyên tố hóa học',
      locator: 'Bài 9, tr. 46',
      excerpt: 'Độ âm điện đặc trưng cho khả năng hút electron; lớn nhất ở nguyên tố nhóm VIIA chu kỳ 2.',
    },
    revisions: [
      {
        revision: 1,
        version: 2,
        content: 'Nguyên tố nào sau đây có độ âm điện lớn nhất trong bảng tuần hoàn?',
        options: ['Flo (F)', 'Oxi (O)', 'Clo (Cl)', 'Nitơ (N)'],
        correctIndex: 0,
        status: 'REJECTED',
        author: 'gv.nguyen.van.a',
        updatedAt: '2026-10-14T16:48:00Z',
      },
    ],
    decisions: [
      {
        id: 'dec-109',
        revision: 1,
        actor: 'tbm.hoa.thpt.a',
        action: 'REJECT',
        reason: 'Cần bổ sung ký hiệu nguyên tố kèm tên tiếng Việt chuẩn chương trình GDPT 2018 (Fluorine).',
        occurredAt: '2026-10-14T16:48:00Z',
      },
    ],
  },
];

const INITIAL_KNOWLEDGE_DOCS: KnowledgeDocument[] = [
  {
    sourceId: 'kb-math12-ch1',
    title: 'SGK Giải tích 12 — Chương I: Ứng dụng đạo hàm để khảo sát và vẽ đồ thị hàm số',
    subject: 'Toán',
    topic: 'Đạo hàm',
    scopeLabel: 'THPT Nguyễn Du · Tổ Toán',
    chunkCount: 84,
    updatedAt: '2026-10-14T08:00:00Z',
    status: 'READY',
  },
  {
    sourceId: 'kb-math12-ch2',
    title: 'SGK Giải tích 12 — Chương II: Hàm số lũy thừa, hàm số mũ và hàm số logarit',
    subject: 'Toán',
    topic: 'Logarit',
    scopeLabel: 'THPT Nguyễn Du · Tổ Toán',
    chunkCount: 72,
    updatedAt: '2026-10-14T08:15:00Z',
    status: 'READY',
  },
  {
    sourceId: 'kb-chem10-ch2',
    title: 'SGK Hóa học 10 — Chương II: Bảng tuần hoàn các nguyên tố hóa học và định luật tuần hoàn',
    subject: 'Hóa học',
    topic: 'Bảng tuần hoàn',
    scopeLabel: 'THPT Nguyễn Du · Tổ Hóa – Sinh',
    chunkCount: 56,
    updatedAt: '2026-10-13T10:30:00Z',
    status: 'READY',
  },
];

const INITIAL_BLUEPRINTS: BlueprintItem[] = [
  {
    id: 'bp-1',
    name: 'Kiểm tra giữa kỳ I – Toán 12 (Ma trận chuẩn 2 điểm)',
    subject: 'Toán',
    grade: 'Khối 12',
    totalPoints: 2,
    durationMinutes: 45,
    author: 'gv.nguyen.van.a',
    updatedAt: '2026-10-16T07:00:00Z',
    slots: [
      { id: 's1', subject: 'Toán', topic: 'Đạo hàm', difficulty: 'EASY', questionCount: 2, pointsPerQuestion: 0.25 },
      { id: 's2', subject: 'Toán', topic: 'Đạo hàm', difficulty: 'MEDIUM', questionCount: 2, pointsPerQuestion: 0.25 },
      { id: 's3', subject: 'Toán', topic: 'Logarit', difficulty: 'MEDIUM', questionCount: 2, pointsPerQuestion: 0.25 },
      { id: 's4', subject: 'Toán', topic: 'Logarit', difficulty: 'HARD', questionCount: 1, pointsPerQuestion: 0.5 },
    ],
  },
  {
    id: 'bp-2',
    name: 'Đề kiểm tra 15 phút – Ứng dụng Đạo hàm (Đủ câu hỏi)',
    subject: 'Toán',
    grade: 'Khối 12',
    totalPoints: 1,
    durationMinutes: 15,
    author: 'gv.nguyen.van.a',
    updatedAt: '2026-10-15T16:00:00Z',
    slots: [
      { id: 's5', subject: 'Toán', topic: 'Đạo hàm', difficulty: 'EASY', questionCount: 2, pointsPerQuestion: 0.25 },
      { id: 's6', subject: 'Toán', topic: 'Logarit', difficulty: 'MEDIUM', questionCount: 2, pointsPerQuestion: 0.25 },
    ],
  },
];

const INITIAL_EXAMS: AssembledExam[] = [
  {
    id: 'ex-01',
    title: 'Đề kiểm tra 15 phút – Giải tích 12 – Mã đề 101',
    blueprintId: 'bp-2',
    blueprintName: 'Đề kiểm tra 15 phút – Ứng dụng Đạo hàm (Đủ câu hỏi)',
    subject: 'Toán',
    scopeLevel: 'SCHOOL',
    status: 'FINALIZED',
    durationMinutes: 15,
    totalPoints: 1,
    author: 'gv.nguyen.van.a',
    confirmedBy: 'tbm.toan.thpt.a',
    finalizedAt: '2026-10-16T10:00:00Z',
    updatedAt: '2026-10-16T10:00:00Z',
    questions: [
      {
        id: 'q-1002',
        revision: 1,
        version: 2,
        content: 'Cho hàm số y = x³ − 3x² + 2. Giá trị cực đại của hàm số bằng bao nhiêu?',
        options: ['2', '−2', '0', '1'],
        correctIndex: 0,
        subject: 'Toán',
        topic: 'Đạo hàm',
        difficulty: 'EASY',
        points: 0.25,
        status: 'APPROVED',
      },
      {
        id: 'q-1003',
        revision: 1,
        version: 2,
        content: 'Tiếp tuyến của đồ thị hàm số y = x³ − 2x tại điểm có hoành độ x₀ = 1 có hệ số góc bằng?',
        options: ['1', '−1', '3', '2'],
        correctIndex: 0,
        subject: 'Toán',
        topic: 'Đạo hàm',
        difficulty: 'EASY',
        points: 0.25,
        status: 'APPROVED',
      },
      {
        id: 'q-1005',
        revision: 1,
        version: 2,
        content: 'Số nghiệm thực phân biệt của phương trình log₂(x² − 1) = 3 là?',
        options: ['2', '1', '0', '3'],
        correctIndex: 0,
        subject: 'Toán',
        topic: 'Logarit',
        difficulty: 'MEDIUM',
        points: 0.25,
        status: 'APPROVED',
      },
      {
        id: 'q-1006',
        revision: 1,
        version: 2,
        content: 'Tập nghiệm của bất phương trình log₀.₅(x − 1) > −2 là?',
        options: ['(1; 5)', '(−∞; 5)', '(5; +∞)', '[1; 5)'],
        correctIndex: 0,
        subject: 'Toán',
        topic: 'Logarit',
        difficulty: 'MEDIUM',
        points: 0.25,
        status: 'APPROVED',
      },
    ],
    issues: [],
  },
  {
    id: 'ex-02',
    title: 'Đề kiểm tra giữa kỳ I – Toán 12 – Bản lắp ráp số 1',
    blueprintId: 'bp-1',
    blueprintName: 'Kiểm tra giữa kỳ I – Toán 12 (Ma trận chuẩn 2 điểm)',
    subject: 'Toán',
    scopeLevel: 'SCHOOL',
    status: 'VALIDATED',
    durationMinutes: 45,
    totalPoints: 1.25,
    author: 'gv.nguyen.van.a',
    updatedAt: '2026-10-16T11:30:00Z',
    questions: [
      {
        id: 'q-1002',
        revision: 1,
        version: 2,
        content: 'Cho hàm số y = x³ − 3x² + 2. Giá trị cực đại của hàm số bằng bao nhiêu?',
        options: ['2', '−2', '0', '1'],
        correctIndex: 0,
        subject: 'Toán',
        topic: 'Đạo hàm',
        difficulty: 'EASY',
        points: 0.25,
        status: 'APPROVED',
      },
      {
        id: 'q-1003',
        revision: 1,
        version: 2,
        content: 'Tiếp tuyến của đồ thị hàm số y = x³ − 2x tại điểm có hoành độ x₀ = 1 có hệ số góc bằng?',
        options: ['1', '−1', '3', '2'],
        correctIndex: 0,
        subject: 'Toán',
        topic: 'Đạo hàm',
        difficulty: 'EASY',
        points: 0.25,
        status: 'APPROVED',
      },
      {
        id: 'q-1004',
        revision: 1,
        version: 2,
        content: 'Hàm số y = −x⁴ + 2x² + 3 đồng biến trên khoảng nào dưới đây?',
        options: ['(−∞; −1) và (0; 1)', '(−1; 0) và (1; +∞)', '(−1; 1)', '(0; +∞)'],
        correctIndex: 0,
        subject: 'Toán',
        topic: 'Đạo hàm',
        difficulty: 'MEDIUM',
        points: 0.25,
        status: 'APPROVED',
      },
      {
        id: 'q-1005',
        revision: 1,
        version: 2,
        content: 'Số nghiệm thực phân biệt của phương trình log₂(x² − 1) = 3 là?',
        options: ['2', '1', '0', '3'],
        correctIndex: 0,
        subject: 'Toán',
        topic: 'Logarit',
        difficulty: 'MEDIUM',
        points: 0.25,
        status: 'APPROVED',
      },
      {
        id: 'q-1006',
        revision: 1,
        version: 2,
        content: 'Tập nghiệm của bất phương trình log₀.₅(x − 1) > −2 là?',
        options: ['(1; 5)', '(−∞; 5)', '(5; +∞)', '[1; 5)'],
        correctIndex: 0,
        subject: 'Toán',
        topic: 'Logarit',
        difficulty: 'MEDIUM',
        points: 0.25,
        status: 'APPROVED',
      },
    ],
    issues: [],
  },
];

const INITIAL_EXAM_EVENTS: ExamSelectionEvent[] = [
  {
    id: 'ev-math12-gk1',
    name: 'Kỳ kiểm tra Giữa học kỳ I — Toán 12 (Năm học 2026–2027)',
    schoolName: 'Trường THPT Nguyễn Du',
    departmentName: 'Tổ Toán – Tin',
    subject: 'Toán',
    grade: 'Khối 12',
    termLabel: 'Giữa kỳ I · 2026–2027',
    selectedSubmissionId: null,
    updatedAt: '2026-10-16T11:30:00Z',
    history: [
      {
        id: 'evh-1',
        occurredAt: '2026-10-16T09:15:00Z',
        actor: 'gv.nguyen.van.a',
        action: 'SUBMITTED',
        submissionId: 'sub-101',
        revision: 1,
        detail: 'Gửi đề dự tuyển Phương án A (4 câu đã duyệt, 1.0 điểm)',
      },
      {
        id: 'evh-2',
        occurredAt: '2026-10-16T10:30:00Z',
        actor: 'gv.tran.thi.b',
        action: 'SUBMITTED',
        submissionId: 'sub-102',
        revision: 1,
        detail: 'Gửi đề dự tuyển Phương án B (4 câu đã duyệt, 1.0 điểm)',
      },
    ],
  },
];

const INITIAL_SUBMISSIONS: ExamCandidateSubmission[] = [
  {
    id: 'sub-101',
    eventId: 'ev-math12-gk1',
    examTitle: 'Đề dự tuyển Giữa kỳ I Toán 12 — Phương án A (GV Nguyễn Văn A)',
    revision: 1,
    expectedVersion: 1,
    teacherUserName: 'gv.nguyen.van.a',
    blueprintName: 'Đề kiểm tra 15 phút – Ứng dụng Đạo hàm (Đủ câu hỏi)',
    questionCount: 4,
    totalPoints: 1.0,
    allQuestionsApproved: true,
    status: 'PENDING_REVIEW',
    submittedAt: '2026-10-16T09:15:00Z',
    questions: INITIAL_EXAMS[0].questions,
  },
  {
    id: 'sub-102',
    eventId: 'ev-math12-gk1',
    examTitle: 'Đề dự tuyển Giữa kỳ I Toán 12 — Phương án B (GV Trần Thị B)',
    revision: 1,
    expectedVersion: 1,
    teacherUserName: 'gv.tran.thi.b',
    blueprintName: 'Đề kiểm tra 15 phút – Ứng dụng Đạo hàm (Đủ câu hỏi)',
    questionCount: 4,
    totalPoints: 1.0,
    allQuestionsApproved: true,
    status: 'PENDING_REVIEW',
    submittedAt: '2026-10-16T10:30:00Z',
    questions: INITIAL_EXAMS[0].questions,
  },
];

const INITIAL_AUDIT: LocalAuditItem[] = [
  {
    id: 'aud-1',
    occurredAt: '2026-10-16T10:00:00Z',
    actor: 'tbm.toan.thpt.a',
    action: 'EXAM_FINALIZED',
    target: 'ex-01',
    detail: 'Chốt đề kiểm tra 15 phút – Giải tích 12 – Mã đề 101 (4 câu @APPROVED)',
    schoolName: 'Trường THPT Nguyễn Du',
    correlationId: 'corr-exam-01',
  },
  {
    id: 'aud-2',
    occurredAt: '2026-10-16T09:40:00Z',
    actor: 'gv.tran.thi.b',
    action: 'REVIEW_SUBMITTED',
    target: 'q-1007@rev1',
    detail: 'Gửi duyệt câu hỏi AI Draft môn Toán / Logarit (expectedVersion=2)',
    schoolName: 'Trường THPT Nguyễn Du',
    correlationId: 'corr-rev-1007',
  },
  {
    id: 'aud-3',
    occurredAt: '2026-10-15T15:30:00Z',
    actor: 'tbm.toan.thpt.a',
    action: 'QUESTION_APPROVED',
    target: 'q-1004@rev1',
    detail: 'Duyệt câu hỏi AI Draft sau kiểm tra trích dẫn SGK Giải tích 12 tr. 9',
    schoolName: 'Trường THPT Nguyễn Du',
    correlationId: 'corr-rev-1004',
  },
  {
    id: 'aud-4',
    occurredAt: '2026-10-15T14:20:00Z',
    actor: 'tbm.toan.thpt.a',
    action: 'QUESTION_REJECTED',
    target: 'q-1001@rev1',
    detail: 'Trả lại: Cần ghi rõ ký hiệu y′ và tập xác định của hàm số',
    schoolName: 'Trường THPT Nguyễn Du',
    correlationId: 'corr-rev-1001',
  },
  {
    id: 'aud-5',
    occurredAt: '2026-10-15T08:00:00Z',
    actor: 'admin.thpt.a',
    action: 'ROLE_GRANTED',
    target: 'tbm.toan.thpt.a',
    detail: 'Cấp vai trò Trưởng Bộ Môn (DepartmentHead) — Tổ Toán – Tin',
    schoolName: 'Trường THPT Nguyễn Du',
    correlationId: 'corr-role-01',
  },
];

let state: WorkflowState = {
  questions: INITIAL_QUESTIONS,
  knowledgeDocs: INITIAL_KNOWLEDGE_DOCS,
  blueprints: INITIAL_BLUEPRINTS,
  exams: INITIAL_EXAMS,
  examEvents: INITIAL_EXAM_EVENTS,
  examSubmissions: INITIAL_SUBMISSIONS,
  auditTrail: INITIAL_AUDIT,
  navIntent: null,
};

const listeners = new Set<() => void>();

function emit() {
  for (const listener of listeners) listener();
}

export function getWorkflowState(): WorkflowState {
  return state;
}

export function subscribeWorkflow(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function useWorkflowStore(): WorkflowState {
  return useSyncExternalStore(subscribeWorkflow, getWorkflowState, getWorkflowState);
}

export function setWorkflowNavIntent(intent: WorkflowNavigationIntent | null) {
  state = { ...state, navIntent: intent };
  emit();
}

export function appendLocalAudit(item: Omit<LocalAuditItem, 'id' | 'occurredAt'>) {
  const entry: LocalAuditItem = {
    ...item,
    id: `aud-${Date.now()}-${Math.floor(Math.random() * 1000)}`,
    occurredAt: new Date().toISOString(),
  };
  state = { ...state, auditTrail: [entry, ...state.auditTrail] };
  emit();
}

export function upsertBankQuestion(params: {
  id?: string;
  content: string;
  options: string[];
  correctIndex: number;
  explanation?: string;
  subject: string;
  topic: string;
  difficulty: Difficulty;
  points: number;
  source?: QuestionSource;
  actorUserName: string;
  submitForReview: boolean;
  citation?: QuestionCitation;
  note?: string;
}): BankQuestion {
  const now = new Date().toISOString();
  const existing = params.id ? state.questions.find(q => q.id === params.id) : undefined;
  const nextStatus: QuestionStatus = params.submitForReview ? 'REVIEW_REQUIRED' : 'DRAFT';

  if (existing) {
    // Immutable revision rule: editing an APPROVED or REJECTED or existing question creates a new revision
    const nextRevision = existing.revision + 1;
    const nextVersion = existing.version + 1;
    const revRecord: QuestionRevisionRecord = {
      revision: nextRevision,
      version: nextVersion,
      content: params.content,
      options: params.options,
      correctIndex: params.correctIndex,
      status: nextStatus,
      author: params.actorUserName,
      updatedAt: now,
      note:
        params.note ??
        (existing.status === 'APPROVED'
          ? `Tạo revision mới từ bản đã duyệt rev ${existing.revision} (bản cũ bất biến)`
          : `Cập nhật thành revision ${nextRevision}`),
    };
    const updated: BankQuestion = {
      ...existing,
      revision: nextRevision,
      version: nextVersion,
      content: params.content,
      options: params.options,
      correctIndex: params.correctIndex,
      explanation: params.explanation ?? existing.explanation,
      subject: params.subject,
      topic: params.topic,
      difficulty: params.difficulty,
      points: params.points,
      status: nextStatus,
      author: params.actorUserName,
      updatedAt: now,
      submittedAt: params.submitForReview ? now : existing.submittedAt,
      rejectReason: undefined,
      citation: params.citation ?? existing.citation,
      revisions: [revRecord, ...existing.revisions],
      decisions: params.submitForReview
        ? [
            ...existing.decisions,
            {
              id: `dec-${Date.now()}`,
              revision: nextRevision,
              actor: params.actorUserName,
              action: 'SUBMIT',
              occurredAt: now,
            },
          ]
        : existing.decisions,
    };
    state = {
      ...state,
      questions: state.questions.map(q => (q.id === existing.id ? updated : q)),
    };
    appendLocalAudit({
      actor: params.actorUserName,
      action: params.submitForReview ? 'REVIEW_SUBMITTED' : 'QUESTION_EDITED',
      target: `${updated.id}@rev${updated.revision}`,
      detail: `${params.submitForReview ? 'Gửi duyệt' : 'Lưu nháp'} revision mới (${updated.subject} / ${updated.topic})`,
      schoolName: 'Trường THPT Nguyễn Du',
    });
    return updated;
  }

  const newId = params.id ?? `q-${Math.floor(1010 + state.questions.length + Math.random() * 900)}`;
  const revRecord: QuestionRevisionRecord = {
    revision: 1,
    version: params.submitForReview ? 2 : 1,
    content: params.content,
    options: params.options,
    correctIndex: params.correctIndex,
    status: nextStatus,
    author: params.actorUserName,
    updatedAt: now,
    note: params.note ?? 'Khởi tạo câu hỏi mới',
  };
  const created: BankQuestion = {
    id: newId,
    revision: 1,
    version: params.submitForReview ? 2 : 1,
    content: params.content,
    options: params.options,
    correctIndex: params.correctIndex,
    explanation: params.explanation,
    subject: params.subject,
    topic: params.topic,
    difficulty: params.difficulty,
    points: params.points,
    status: nextStatus,
    source: params.source ?? 'MANUAL',
    ownerUserName: params.actorUserName,
    author: params.actorUserName,
    updatedAt: now,
    submittedAt: params.submitForReview ? now : undefined,
    citation: params.citation,
    revisions: [revRecord],
    decisions: params.submitForReview
      ? [
          {
            id: `dec-${Date.now()}`,
            revision: 1,
            actor: params.actorUserName,
            action: 'SUBMIT',
            occurredAt: now,
          },
        ]
      : [],
  };
  state = {
    ...state,
    questions: [created, ...state.questions],
  };
  appendLocalAudit({
    actor: params.actorUserName,
    action: params.submitForReview ? 'REVIEW_SUBMITTED' : 'QUESTION_CREATED',
    target: `${created.id}@rev1`,
    detail: `Tạo câu hỏi (${created.source}) môn ${created.subject} / ${created.topic} — trạng thái ${created.status}`,
    schoolName: 'Trường THPT Nguyễn Du',
  });
  return created;
}

export function submitDraftQuestionForReview(questionId: string, actorUserName: string): boolean {
  const target = state.questions.find(q => q.id === questionId);
  if (!target || target.status !== 'DRAFT') return false;
  const now = new Date().toISOString();
  const nextVersion = target.version + 1;
  const updated: BankQuestion = {
    ...target,
    status: 'REVIEW_REQUIRED',
    version: nextVersion,
    updatedAt: now,
    submittedAt: now,
    revisions: target.revisions.map((r, idx) =>
      idx === 0 ? { ...r, status: 'REVIEW_REQUIRED', version: nextVersion, updatedAt: now } : r,
    ),
    decisions: [
      ...target.decisions,
      {
        id: `dec-${Date.now()}`,
        revision: target.revision,
        actor: actorUserName,
        action: 'SUBMIT',
        occurredAt: now,
      },
    ],
  };
  state = {
    ...state,
    questions: state.questions.map(q => (q.id === questionId ? updated : q)),
  };
  appendLocalAudit({
    actor: actorUserName,
    action: 'REVIEW_SUBMITTED',
    target: `${updated.id}@rev${updated.revision}`,
    detail: `Nộp câu hỏi vào hàng chờ duyệt (v${updated.version})`,
    schoolName: 'Trường THPT Nguyễn Du',
  });
  return true;
}

export function decideQuestionReviewInStore(params: {
  questionId: string;
  revision: number;
  expectedVersion: number;
  reviewerUserName: string;
  approve: boolean;
  reason?: string;
}): { ok: boolean; code?: 'SELF_REVIEW' | 'CONFLICT' | 'REASON_REQUIRED' | 'NOT_FOUND'; message: string } {
  const q = state.questions.find(item => item.id === params.questionId);
  if (!q) return { ok: false, code: 'NOT_FOUND', message: 'Không tìm thấy câu hỏi' };

  if (q.ownerUserName === params.reviewerUserName || q.author === params.reviewerUserName) {
    return {
      ok: false,
      code: 'SELF_REVIEW',
      message: 'Vi phạm quy tắc M3-03: Người tạo hoặc sở hữu câu hỏi không được tự duyệt (Self-review bị chặn).',
    };
  }

  if (q.status !== 'REVIEW_REQUIRED' || q.revision !== params.revision || q.version !== params.expectedVersion) {
    return {
      ok: false,
      code: 'CONFLICT',
      message: `Xung đột phiên bản (HTTP 409): Revision hiện tại là rev ${q.revision} (v${q.version}), trạng thái ${q.status}.`,
    };
  }

  if (!params.approve && !params.reason?.trim()) {
    return {
      ok: false,
      code: 'REASON_REQUIRED',
      message: 'Vui lòng nhập lý do trả lại (bắt buộc khi từ chối).',
    };
  }

  const now = new Date().toISOString();
  const nextStatus: QuestionStatus = params.approve ? 'APPROVED' : 'REJECTED';
  const nextVersion = q.version + 1;
  const updated: BankQuestion = {
    ...q,
    status: nextStatus,
    version: nextVersion,
    updatedAt: now,
    rejectReason: params.approve ? undefined : params.reason?.trim(),
    revisions: q.revisions.map((r, idx) =>
      idx === 0 ? { ...r, status: nextStatus, version: nextVersion, updatedAt: now } : r,
    ),
    decisions: [
      ...q.decisions,
      {
        id: `dec-${Date.now()}`,
        revision: q.revision,
        actor: params.reviewerUserName,
        action: params.approve ? 'APPROVE' : 'REJECT',
        reason: params.approve ? undefined : params.reason?.trim(),
        occurredAt: now,
      },
    ],
  };

  state = {
    ...state,
    questions: state.questions.map(item => (item.id === q.id ? updated : item)),
  };

  appendLocalAudit({
    actor: params.reviewerUserName,
    action: params.approve ? 'QUESTION_APPROVED' : 'QUESTION_REJECTED',
    target: `${q.id}@rev${q.revision}`,
    detail: params.approve
      ? `Phê duyệt câu hỏi ${q.subject} / ${q.topic} (v${nextVersion})`
      : `Trả lại câu hỏi: ${params.reason?.trim()}`,
    schoolName: 'Trường THPT Nguyễn Du',
  });

  return {
    ok: true,
    message: params.approve ? 'Đã phê duyệt câu hỏi vào ngân hàng chung' : 'Đã trả lại câu hỏi kèm lý do',
  };
}

export function deleteDraftQuestionFromStore(questionId: string) {
  state = {
    ...state,
    questions: state.questions.filter(q => !(q.id === questionId && q.status === 'DRAFT')),
  };
  emit();
}

export function saveBlueprintToStore(bp: BlueprintItem) {
  const exists = state.blueprints.some(item => item.id === bp.id);
  state = {
    ...state,
    blueprints: exists
      ? state.blueprints.map(item => (item.id === bp.id ? bp : item))
      : [bp, ...state.blueprints],
  };
  emit();
}

export function computeSlotStats(slot: BlueprintSlotItem, questions: BankQuestion[]) {
  const matching = questions.filter(
    q =>
      q.subject.toLowerCase() === slot.subject.toLowerCase() &&
      q.topic.toLowerCase() === slot.topic.toLowerCase() &&
      q.difficulty === slot.difficulty,
  );
  const approved = matching.filter(q => q.status === 'APPROVED');
  const pending = matching.filter(q => q.status === 'REVIEW_REQUIRED');
  const draft = matching.filter(q => q.status === 'DRAFT');
  const missingCount = Math.max(0, slot.questionCount - approved.length);
  return {
    approvedCount: approved.length,
    pendingCount: pending.length,
    draftCount: draft.length,
    missingCount,
    approvedQuestions: approved,
    allMatchingQuestions: matching,
  };
}

export function assembleExamFromBlueprint(
  bp: BlueprintItem,
  actorUserName: string,
  scopeLevel: 'SCHOOL' | 'MINISTRY' = 'SCHOOL',
): { exam: AssembledExam; ok: boolean } {
  const usedIds = new Set<string>();
  const picked: PinnedExamQuestion[] = [];
  const issues: string[] = [];

  for (const slot of bp.slots) {
    const candidates = state.questions.filter(
      q =>
        q.status === 'APPROVED' &&
        !usedIds.has(q.id) &&
        q.subject.toLowerCase() === slot.subject.toLowerCase() &&
        q.topic.toLowerCase() === slot.topic.toLowerCase() &&
        q.difficulty === slot.difficulty,
    );

    const selectedForSlot = candidates.slice(0, slot.questionCount);
    selectedForSlot.forEach(q => {
      usedIds.add(q.id);
      picked.push({
        id: q.id,
        revision: q.revision,
        version: q.version,
        content: q.content,
        options: q.options,
        correctIndex: q.correctIndex,
        subject: q.subject,
        topic: q.topic,
        difficulty: q.difficulty,
        points: slot.pointsPerQuestion,
        status: 'APPROVED',
      });
    });

    if (selectedForSlot.length < slot.questionCount) {
      issues.push(
        `Ô "${slot.topic} (${slot.difficulty})" yêu cầu ${slot.questionCount} câu APPROVED nhưng chỉ có ${selectedForSlot.length} câu.`,
      );
    }
  }

  const currentPoints = Math.round(picked.reduce((s, q) => s + q.points, 0) * 100) / 100;
  if (currentPoints !== bp.totalPoints) {
    issues.push(`Tổng điểm câu hỏi đã chọn (${currentPoints}đ) chưa khớp tổng điểm ma trận (${bp.totalPoints}đ).`);
  }

  const status: ExamAssemblyStatus = issues.length === 0 ? 'VALIDATED' : 'ASSEMBLING';
  const exam: AssembledExam = {
    id: `ex-${Date.now().toString(36)}`,
    title: `${bp.name} — Bản ráp mới`,
    blueprintId: bp.id,
    blueprintName: bp.name,
    subject: bp.subject,
    scopeLevel,
    status,
    durationMinutes: bp.durationMinutes,
    totalPoints: bp.totalPoints,
    author: actorUserName,
    updatedAt: new Date().toISOString(),
    questions: picked,
    issues,
  };

  state = {
    ...state,
    exams: [exam, ...state.exams],
  };
  emit();
  return { exam, ok: issues.length === 0 };
}

export function finalizeExamInStore(
  examId: string,
  actorUserName: string,
): { ok: boolean; message: string } {
  const exam = state.exams.find(e => e.id === examId);
  if (!exam) return { ok: false, message: 'Không tìm thấy đề thi' };
  if (exam.status === 'FINALIZED') {
    return { ok: false, message: 'Đề thi này đã được chốt thành snapshot bất biến.' };
  }
  if (exam.issues.length > 0) {
    return {
      ok: false,
      message: `Không thể chốt đề khi còn lỗi ma trận: ${exam.issues[0]}`,
    };
  }
  if (exam.questions.some(q => q.status !== 'APPROVED')) {
    return {
      ok: false,
      message: 'Mọi câu hỏi trong đề phải ở trạng thái APPROVED trước khi chốt.',
    };
  }
  if (exam.scopeLevel === 'MINISTRY' && exam.author === actorUserName) {
    return {
      ok: false,
      message: 'Quy tắc đề cấp Bộ: Người xác nhận phải là tài khoản khác với tác giả soạn đề.',
    };
  }

  const now = new Date().toISOString();
  const updated: AssembledExam = {
    ...exam,
    status: 'FINALIZED',
    confirmedBy: actorUserName,
    finalizedAt: now,
    updatedAt: now,
  };

  state = {
    ...state,
    exams: state.exams.map(e => (e.id === examId ? updated : e)),
  };

  appendLocalAudit({
    actor: actorUserName,
    action: 'EXAM_FINALIZED',
    target: `${exam.id}`,
    detail: `Chốt snapshot bất biến "${exam.title}" (${exam.questions.length} câu, ${exam.totalPoints} điểm)`,
    schoolName: 'Trường THPT Nguyễn Du',
  });

  return { ok: true, message: 'Đã chốt đề thi — Snapshot bất biến đã được khóa phiên bản câu hỏi.' };
}

export function submitExamToEventInStore(params: {
  exam: AssembledExam;
  eventId: string;
  teacherUserName: string;
}): { ok: boolean; message: string } {
  const ev = state.examEvents.find(e => e.id === params.eventId);
  if (!ev) return { ok: false, message: 'Không tìm thấy đợt kiểm tra' };
  if (params.exam.issues.length > 0 || params.exam.questions.some(q => q.status !== 'APPROVED')) {
    return {
      ok: false,
      message: 'Đề gửi xét chọn phải đủ ma trận và toàn bộ câu hỏi đều đã APPROVED.',
    };
  }

  const now = new Date().toISOString();
  // Supersede previous PENDING_REVIEW submission of the same teacher in the same event
  const existingTeacherSubmissions = state.examSubmissions.filter(
    s => s.eventId === params.eventId && s.teacherUserName === params.teacherUserName,
  );
  const nextRevision =
    existingTeacherSubmissions.reduce((max, s) => Math.max(max, s.revision), 0) + 1;

  const updatedSubmissions: ExamCandidateSubmission[] = state.examSubmissions.map(s => {
    if (
      s.eventId === params.eventId &&
      s.teacherUserName === params.teacherUserName &&
      s.status === 'PENDING_REVIEW'
    ) {
      return { ...s, status: 'SUPERSEDED', expectedVersion: s.expectedVersion + 1 };
    }
    return s;
  });

  const newSub: ExamCandidateSubmission = {
    id: `sub-${Date.now().toString(36)}`,
    eventId: params.eventId,
    examTitle: `${params.exam.title} (rev ${nextRevision})`,
    revision: nextRevision,
    expectedVersion: 1,
    teacherUserName: params.teacherUserName,
    blueprintName: params.exam.blueprintName,
    questionCount: params.exam.questions.length,
    totalPoints: params.exam.totalPoints,
    allQuestionsApproved: true,
    status: 'PENDING_REVIEW',
    submittedAt: now,
    questions: params.exam.questions,
  };

  state = {
    ...state,
    examSubmissions: [newSub, ...updatedSubmissions],
    examEvents: state.examEvents.map(e =>
      e.id === params.eventId
        ? {
            ...e,
            updatedAt: now,
            history: [
              {
                id: `evh-${Date.now()}`,
                occurredAt: now,
                actor: params.teacherUserName,
                action: 'SUBMITTED',
                submissionId: newSub.id,
                revision: nextRevision,
                detail: `Nộp phiên bản rev ${nextRevision} (${newSub.questionCount} câu APPROVED) — các bản đang chờ trước đó chuyển sang SUPERSEDED`,
              },
              ...e.history,
            ],
          }
        : e,
    ),
  };
  emit();
  return {
    ok: true,
    message: `Đã gửi đề dự tuyển rev ${nextRevision} vào đợt kiểm tra "${ev.name}".`,
  };
}

export function decideExamEventSelectionInStore(params: {
  submissionId: string;
  expectedVersion: number;
  reviewerUserName: string;
  decision: 'SELECT' | 'REJECT';
  reason?: string;
}): { ok: boolean; message: string } {
  const target = state.examSubmissions.find(s => s.id === params.submissionId);
  if (!target) return { ok: false, message: 'Không tìm thấy bản đề dự tuyển.' };

  if (target.teacherUserName === params.reviewerUserName) {
    return {
      ok: false,
      message: 'Vi phạm phân quyền: Người tạo đề không được tự xét chọn đề của chính mình.',
    };
  }
  if (target.expectedVersion !== params.expectedVersion) {
    return {
      ok: false,
      message: 'Xung đột phiên bản (HTTP 409): Đề dự tuyển đã được cập nhật hoặc xử lý bởi người khác.',
    };
  }
  if (!target.allQuestionsApproved) {
    return {
      ok: false,
      message: 'Không thể chọn đề khi còn câu hỏi chưa ở trạng thái APPROVED.',
    };
  }
  if (params.decision === 'REJECT' && !params.reason?.trim()) {
    return {
      ok: false,
      message: 'Vui lòng nhập lý do trả lại đề.',
    };
  }

  const now = new Date().toISOString();

  if (params.decision === 'SELECT') {
    // Atomic rule: Selected submission -> APPROVED; other PENDING_REVIEW or previously APPROVED in same event -> NOT_SELECTED
    const nextSubmissions: ExamCandidateSubmission[] = state.examSubmissions.map(s => {
      if (s.eventId !== target.eventId) return s;
      if (s.id === target.id) {
        return {
          ...s,
          status: 'APPROVED',
          expectedVersion: s.expectedVersion + 1,
          decidedBy: params.reviewerUserName,
          decidedAt: now,
          rejectReason: undefined,
        };
      }
      if (s.status === 'PENDING_REVIEW' || s.status === 'APPROVED') {
        return {
          ...s,
          status: 'NOT_SELECTED',
          expectedVersion: s.expectedVersion + 1,
        };
      }
      return s;
    });

    state = {
      ...state,
      examSubmissions: nextSubmissions,
      examEvents: state.examEvents.map(e =>
        e.id === target.eventId
          ? {
              ...e,
              selectedSubmissionId: target.id,
              updatedAt: now,
              history: [
                {
                  id: `evh-${Date.now()}`,
                  occurredAt: now,
                  actor: params.reviewerUserName,
                  action: 'SELECTED',
                  submissionId: target.id,
                  revision: target.revision,
                  detail: `Chọn đề chính thức cho đợt kiểm tra (con trỏ ExamEvent → ${target.id}@rev${target.revision}); các đề cạnh tranh còn lại chuyển sang NOT_SELECTED`,
                },
                ...e.history,
              ],
            }
          : e,
      ),
    };

    appendLocalAudit({
      actor: params.reviewerUserName,
      action: 'EXAM_SELECTED',
      target: `${target.id}@rev${target.revision}`,
      detail: `Chọn đề chính thức cho đợt kiểm tra ${target.eventId}`,
      schoolName: 'Trường THPT Nguyễn Du',
    });

    return {
      ok: true,
      message: 'Đã chọn đề chính thức cho đợt kiểm tra. Các đề cạnh tranh khác được chuyển sang NOT_SELECTED.',
    };
  }

  // REJECT
  const nextSubmissions: ExamCandidateSubmission[] = state.examSubmissions.map(s =>
    s.id === target.id
      ? {
          ...s,
          status: 'REJECTED',
          expectedVersion: s.expectedVersion + 1,
          decidedBy: params.reviewerUserName,
          decidedAt: now,
          rejectReason: params.reason?.trim(),
        }
      : s,
  );

  state = {
    ...state,
    examSubmissions: nextSubmissions,
    examEvents: state.examEvents.map(e =>
      e.id === target.eventId
        ? {
            ...e,
            selectedSubmissionId: e.selectedSubmissionId === target.id ? null : e.selectedSubmissionId,
            updatedAt: now,
            history: [
              {
                id: `evh-${Date.now()}`,
                occurredAt: now,
                actor: params.reviewerUserName,
                action: 'REJECTED',
                submissionId: target.id,
                revision: target.revision,
                detail: `Trả lại đề kèm lý do: ${params.reason?.trim()}`,
              },
              ...e.history,
            ],
          }
        : e,
    ),
  };

  appendLocalAudit({
    actor: params.reviewerUserName,
    action: 'EXAM_RETURNED',
    target: `${target.id}@rev${target.revision}`,
    detail: `Trả lại đề dự tuyển: ${params.reason?.trim()}`,
    schoolName: 'Trường THPT Nguyễn Du',
  });

  return { ok: true, message: 'Đã trả lại đề dự tuyển kèm lý do.' };
}
