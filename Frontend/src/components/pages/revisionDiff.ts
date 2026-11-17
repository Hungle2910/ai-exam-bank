import { DIFF, type RevisionDetail } from './questionLabels';

export interface FieldDiff { key: string; label: string; before: string; after: string; changed: boolean }

const OPTION_LABELS = 'ABCDEFGH';
const EMPTY = '—';

/**
 * Field-by-field comparison of two revisions of one question. Content only: status and authorship are
 * metadata of a revision, not part of what an author edits.
 */
export function compareRevisions(older: RevisionDetail, newer: RevisionDetail): FieldDiff[] {
  const field = (key: string, label: string, before: string | null, after: string | null): FieldDiff => {
    const b = before?.trim() ? before : EMPTY;
    const a = after?.trim() ? after : EMPTY;
    return { key, label, before: b, after: a, changed: b !== a };
  };

  const optionCount = Math.max(older.options.length, newer.options.length);
  const options = Array.from({ length: optionCount }, (_, i) =>
    field(`option-${i}`, `Phương án ${OPTION_LABELS[i] ?? i + 1}`, older.options[i] ?? null, newer.options[i] ?? null));

  return [
    field('stem', 'Nội dung', older.stem, newer.stem),
    ...options,
    field('correctOption', 'Đáp án đúng', older.correctOption, newer.correctOption),
    field('topic', 'Chủ đề', older.topic.name, newer.topic.name),
    field('difficulty', 'Độ khó', DIFF[older.difficulty][0], DIFF[newer.difficulty][0]),
    field('source', 'Nguồn', older.source, newer.source),
  ];
}
