import { cleanup, fireEvent, render, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import ReviewPage from './ReviewPage';

<<<<<<< HEAD
vi.mock('@/contexts/AuthContext', () => ({
  useAuth: () => ({ csrfToken: 'test-csrf' }),
}));

vi.mock('@/contexts/ToastContext', () => ({
  useToast: () => ({ success: vi.fn(), error: vi.fn() }),
}));
=======
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));

vi.mock('@/contexts/AuthContext', () => ({
  useAuth: () => ({ csrfToken: 'test-csrf' }),
}));
vi.mock('@/contexts/ToastContext', () => ({ useToast: () => toast }));
>>>>>>> origin/dev

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
<<<<<<< HEAD
});

it('does not show mock review decisions when the review API is unavailable', async () => {
=======
  vi.clearAllMocks();
});

const item = {
  id: 'question-1', revision: 2, version: 7, content: 'What is 2 + 2?',
  options: ['3', '4', '5', '6'], correctIndex: 1, subject: 'Math', topic: 'Arithmetic',
  difficulty: 'EASY', points: 1, author: 'Teacher', submittedAt: '2026-11-01T00:00:00Z',
  status: 'REVIEW_REQUIRED', source: 'MANUAL',
};

it('does not offer a mock decision when the pending API is unavailable', async () => {
>>>>>>> origin/dev
  const fetchMock = vi.fn().mockResolvedValue({ ok: false, status: 404 });
  vi.stubGlobal('fetch', fetchMock);

  const view = render(<ReviewPage />);
<<<<<<< HEAD

  await waitFor(() => expect(view.getByText('Không tải được hàng chờ duyệt. Vui lòng thử lại.')).toBeDefined());
  expect(view.queryByText('Duyệt')).toBeNull();
  expect(view.queryByText('Đã duyệt câu hỏi')).toBeNull();
  expect(fetchMock).toHaveBeenCalledWith('/api/v1/reviews/pending', { credentials: 'include' });
});

it('rejects a malformed review response instead of rendering an action', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => [{ id: 'fake', status: 'REVIEW_REQUIRED' }] }));
  const view = render(<ReviewPage />);
  await waitFor(() => expect(view.getByText('Không tải được hàng chờ duyệt. Vui lòng thử lại.')).toBeDefined());
  expect(view.queryByText('Duyệt')).toBeNull();
});

it('keeps a pending revision pending when the decision API denies access', async () => {
  const item = {
    id: 'q-1', revision: 1, version: 2, content: '<script>unsafe</script>',
    options: ['A', 'B', 'C', 'D'], correctIndex: 0,
    subject: 'Toán', topic: 'Số học', difficulty: 'EASY',
    author: 'teacher', submittedAt: '2026-10-16T08:12:00Z',
    status: 'REVIEW_REQUIRED', source: 'AI_DRAFT',
  };
  const fetchMock = vi.fn()
    .mockResolvedValueOnce({ ok: true, json: async () => [item] })
    .mockResolvedValueOnce({ ok: false, status: 403 });
  vi.stubGlobal('fetch', fetchMock);
  const view = render(<ReviewPage />);
  await waitFor(() => expect(view.getByText('Duyệt')).toBeDefined());
  expect(view.container.querySelector('script')).toBeNull();
  fireEvent.click(view.getByText('Duyệt'));
  await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
  expect(fetchMock).toHaveBeenLastCalledWith('/api/v1/reviews/q-1/revisions/1/decision',
    expect.objectContaining({ method: 'POST', body: JSON.stringify({ decision: 'Approve', expectedVersion: 2, reason: null }) }));
  expect(view.getByText('Duyệt')).toBeDefined();
  expect(view.container.querySelector('.tab-count')?.textContent).toBe('1');
=======
  await waitFor(() => expect(view.getByText('Không tải được hàng chờ duyệt. Vui lòng thử lại.')).toBeDefined());
  expect(view.queryByText('Duyệt')).toBeNull();
  expect(fetchMock).toHaveBeenCalledWith('/api/v1/reviews/pending?page=1&pageSize=20', { credentials: 'include' });
});

it('sends the selected revision and version, then reloads the pending list', async () => {
  const fetchMock = vi.fn()
    .mockResolvedValueOnce({ ok: true, json: async () => [item] })
    .mockResolvedValueOnce({ ok: true, status: 200 })
    .mockResolvedValueOnce({ ok: true, json: async () => [] });
  vi.stubGlobal('fetch', fetchMock);

  const view = render(<ReviewPage />);
  await waitFor(() => expect(view.getAllByText('What is 2 + 2?').length).toBe(2));
  fireEvent.click(view.getByRole('button', { name: 'Duyệt' }));
  expect(fetchMock).toHaveBeenCalledTimes(1);
  fireEvent.click(view.getByRole('button', { name: 'Xác nhận duyệt revision 2' }));

  await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(3));
  const [url, init] = fetchMock.mock.calls[1];
  expect(url).toBe('/api/v1/reviews/question-1/revisions/2/decision');
  expect(init.headers['X-CSRF-TOKEN']).toBe('test-csrf');
  expect(JSON.parse(init.body)).toEqual({ decision: 'Approve', expectedVersion: 7 });
  await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Đã duyệt câu hỏi'));
});

it('reloads after a stale decision and never reports success', async () => {
  const fetchMock = vi.fn()
    .mockResolvedValueOnce({ ok: true, json: async () => [item] })
    .mockResolvedValueOnce({ ok: false, status: 409 })
    .mockResolvedValueOnce({ ok: true, json: async () => [] });
  vi.stubGlobal('fetch', fetchMock);

  const view = render(<ReviewPage />);
  await waitFor(() => expect(view.getAllByText('What is 2 + 2?').length).toBe(2));
  fireEvent.click(view.getByRole('button', { name: 'Duyệt' }));
  fireEvent.click(view.getByRole('button', { name: 'Xác nhận duyệt revision 2' }));

  await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(3));
  expect(toast.error).toHaveBeenCalledWith('Câu hỏi đã được người khác xử lý. Đang tải lại…');
  expect(toast.success).not.toHaveBeenCalled();
});

it('fails closed when the pending API returns an invalid revision', async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => [{ ...item, version: 0 }] });
  vi.stubGlobal('fetch', fetchMock);

  const view = render(<ReviewPage />);
  await waitFor(() => expect(view.getByText('Không tải được hàng chờ duyệt. Vui lòng thử lại.')).toBeDefined());
  expect(view.queryByRole('button', { name: 'Duyệt' })).toBeNull();
  expect(fetchMock).toHaveBeenCalledTimes(1);
>>>>>>> origin/dev
});
