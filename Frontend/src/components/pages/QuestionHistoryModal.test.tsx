import { cleanup, fireEvent, render, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import QuestionHistoryModal from './QuestionHistoryModal';
import { compareRevisions } from './revisionDiff';
import type { QuestionHistory, RevisionDetail } from './questionLabels';

const mocks = vi.hoisted(() => ({ apiFetch: vi.fn() }));

vi.mock('@/contexts/AuthContext', () => ({
  useAuth: () => ({ apiFetch: mocks.apiFetch }),
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const json = (status: number, body: unknown = null) => Promise.resolve({ ok: status >= 200 && status < 300, status, json: async () => body });

const revision = (n: number, patch: Partial<RevisionDetail> = {}): RevisionDetail => ({
  revision: n, status: 'DRAFT', origin: 'MANUAL', stem: 'Nội dung gốc', options: ['a', 'b', 'c', 'd'], correctOption: 'A',
  topic: { id: 't1', name: 'Chủ đề 1' }, difficulty: 'EASY', source: null, importBatchId: null, importRowNumber: null,
  createdByUserId: 'u1', createdAt: '2026-11-01T00:00:00Z', isLatest: false, isApprovedInUse: false, ...patch,
});

// Newest first, as the API returns it: 2 is an unreviewed draft, 1 is the approved revision exams still use.
const history: QuestionHistory = {
  id: 'q-1', subject: { id: 's1', name: 'Môn' }, latestRevision: 2, approvedRevision: 1,
  revisions: [
    revision(2, { stem: 'Nội dung đã sửa', correctOption: 'B', isLatest: true }),
    revision(1, { status: 'APPROVED', isApprovedInUse: true }),
  ],
};

it('reports only the fields that changed', () => {
  const diffs = compareRevisions(history.revisions[1], history.revisions[0]);

  expect(diffs.filter(d => d.changed).map(d => d.key)).toEqual(['stem', 'correctOption']);
  expect(diffs.find(d => d.key === 'stem')).toMatchObject({ before: 'Nội dung gốc', after: 'Nội dung đã sửa' });
});

it('treats a missing option or source as an empty value, not as unchanged text', () => {
  const diffs = compareRevisions(revision(1, { options: ['a', 'b'] }), revision(2, { options: ['a', 'b', 'c'], source: 'SGK' }));

  expect(diffs.find(d => d.key === 'option-2')).toMatchObject({ before: '—', after: 'c', changed: true });
  expect(diffs.find(d => d.key === 'source')).toMatchObject({ before: '—', after: 'SGK', changed: true });
});

it('loads the history from the API and compares the newest revision with the one before it', async () => {
  mocks.apiFetch.mockImplementation(() => json(200, history));

  const view = render(<QuestionHistoryModal id="q-1" mockRevisions={null} onClose={() => {}} />);

  await view.findByText('Đang dùng để thi');
  expect(mocks.apiFetch.mock.calls[0][0]).toBe('/api/v1/questions/q-1/revisions');
  const compare = view.container.querySelector('#revision-compare')!;
  expect(compare.textContent).toContain('2 trường khác nhau');
  expect(compare.querySelectorAll('[data-changed="true"]').length).toBe(2);
});

it('shows one revision on its own when no comparison is chosen', async () => {
  mocks.apiFetch.mockImplementation(() => json(200, history));
  const view = render(<QuestionHistoryModal id="q-1" mockRevisions={null} onClose={() => {}} />);
  await view.findByText('Đang dùng để thi');

  fireEvent.change(view.container.querySelector('#select-compare')!, { target: { value: '' } });

  expect(view.container.querySelector('#revision-compare')).toBeNull();
  expect(view.container.querySelector('#revision-view')!.textContent).toContain('Nội dung đã sửa');
});

it('opens an older revision against the one before it, and has no comparison for the first revision', async () => {
  mocks.apiFetch.mockImplementation(() => json(200, history));
  const view = render(<QuestionHistoryModal id="q-1" mockRevisions={null} onClose={() => {}} />);
  await view.findByText('Đang dùng để thi');

  fireEvent.click(view.container.querySelector('#btn-revision-1')!);

  expect(view.container.querySelector('#revision-compare')).toBeNull();
  expect(view.container.querySelector('#revision-view')!.textContent).toContain('Nội dung gốc');
});

it('shows a retry when the API refuses and a plain message for a question outside the caller scope', async () => {
  mocks.apiFetch.mockImplementation(() => json(404));
  const view = render(<QuestionHistoryModal id="q-1" mockRevisions={null} onClose={() => {}} />);

  expect((await view.findByRole('alert')).textContent).toContain('Không tìm thấy câu hỏi');
  fireEvent.click(view.container.querySelector('#btn-retry-history')!);
  await waitFor(() => expect(mocks.apiFetch).toHaveBeenCalledTimes(2));
});

it('builds the history from sample data without calling the API', async () => {
  const view = render(<QuestionHistoryModal id="q-1" mockRevisions={[...history.revisions].reverse()} onClose={() => {}} />);

  await view.findByText('Mới nhất');
  expect(mocks.apiFetch).not.toHaveBeenCalled();
});

// Revision 2 is the newest and still a draft; revision 1 is approved.
const draftHead: QuestionHistory = {
  ...history,
  revisions: [revision(2, { isLatest: true }), revision(1, { status: 'APPROVED', isApprovedInUse: true })],
};

it('submits the newest draft for review and reloads the history once the server accepted it', async () => {
  mocks.apiFetch.mockImplementation(() => json(200, draftHead));
  const onSubmitReview = vi.fn().mockResolvedValue(true);
  const view = render(<QuestionHistoryModal id="q-1" mockRevisions={null} onClose={() => {}} onSubmitReview={onSubmitReview} />);

  fireEvent.click(await view.findByText('Gửi duyệt'));

  await waitFor(() => expect(onSubmitReview).toHaveBeenCalledWith(2));
  await waitFor(() => expect(mocks.apiFetch).toHaveBeenCalledTimes(2));
});

it('does not reload when the submission is refused', async () => {
  mocks.apiFetch.mockImplementation(() => json(200, draftHead));
  const onSubmitReview = vi.fn().mockResolvedValue(false);
  const view = render(<QuestionHistoryModal id="q-1" mockRevisions={null} onClose={() => {}} onSubmitReview={onSubmitReview} />);

  fireEvent.click(await view.findByText('Gửi duyệt'));

  await waitFor(() => expect(onSubmitReview).toHaveBeenCalledTimes(1));
  expect(mocks.apiFetch).toHaveBeenCalledTimes(1);
});

it('offers no submit action for an older revision, a revision under review, or a caller who cannot submit', async () => {
  mocks.apiFetch.mockImplementation(() => json(200, draftHead));
  const onSubmitReview = vi.fn();
  const view = render(<QuestionHistoryModal id="q-1" mockRevisions={null} onClose={() => {}} onSubmitReview={onSubmitReview} />);
  await view.findByText('Gửi duyệt');
  fireEvent.click(view.container.querySelector('#btn-revision-1')!);
  expect(view.queryByText('Gửi duyệt')).toBeNull();
  cleanup();

  mocks.apiFetch.mockImplementation(() => json(200, { ...draftHead, revisions: [revision(2, { isLatest: true, status: 'PENDING_REVIEW' }), draftHead.revisions[1]] }));
  const pending = render(<QuestionHistoryModal id="q-1" mockRevisions={null} onClose={() => {}} onSubmitReview={onSubmitReview} />);
  await pending.findByText('Mới nhất');
  expect(pending.queryByText('Gửi duyệt')).toBeNull();
  cleanup();

  mocks.apiFetch.mockImplementation(() => json(200, draftHead));
  const viewer = render(<QuestionHistoryModal id="q-1" mockRevisions={null} onClose={() => {}} />);
  await viewer.findByText('Mới nhất');
  expect(viewer.queryByText('Gửi duyệt')).toBeNull();
});
