/** Types and display labels shared by the question list, editor and revision history. */

export type Difficulty = 'EASY' | 'MEDIUM' | 'HARD';
export type QStatus = 'DRAFT' | 'PENDING_REVIEW' | 'APPROVED' | 'REJECTED';
export type Origin = 'MANUAL' | 'IMPORT' | 'AI_DRAFT' | 'SEED';

export interface Ref { id: string; name: string }

/** One revision with its full content, as returned by GET /questions/{id}/revisions. */
export interface RevisionDetail {
  revision: number; status: QStatus; origin: Origin;
  stem: string; options: string[]; correctOption: string;
  topic: Ref; difficulty: Difficulty; source: string | null;
  importBatchId: string | null; importRowNumber: number | null;
  createdByUserId: string; createdAt: string;
  isLatest: boolean; isApprovedInUse: boolean;
}

export interface QuestionHistory {
  id: string; subject: Ref; latestRevision: number; approvedRevision: number | null; revisions: RevisionDetail[];
}

export const DIFF: Record<Difficulty, [string, string]> = {
  EASY: ['Dễ', 'badge-green'], MEDIUM: ['Trung bình', 'badge-amber'], HARD: ['Khó', 'badge-red'],
};
export const STATUS: Record<QStatus, [string, string]> = {
  DRAFT: ['Nháp', 'badge-gray'], PENDING_REVIEW: ['Chờ duyệt', 'badge-blue'], APPROVED: ['Đã duyệt', 'badge-green'], REJECTED: ['Bị trả lại', 'badge-red'],
};
export const ORIGIN: Record<Origin, string> = { MANUAL: 'Nhập tay', IMPORT: 'Import', AI_DRAFT: 'AI nháp', SEED: 'Dữ liệu mẫu' };
