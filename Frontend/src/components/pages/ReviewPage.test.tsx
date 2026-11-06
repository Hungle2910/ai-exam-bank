import { cleanup, fireEvent, render, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import ReviewPage from './ReviewPage';

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));

vi.mock('@/contexts/AuthContext', () => ({
  useAuth: () => ({ csrfToken: 'test-csrf' }),
}));
vi.mock('@/contexts/ToastContext', () => ({ useToast: () => toast }));

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

const item = {
  id: 'question-1', revision: 2, version: 7, content: 'What is 2 + 2?',
  options: ['3', '4'], correctIndex: 1, subject: 'Math', topic: 'Arithmetic',
  difficulty: 'EASY', points: 1, author: 'Teacher', submittedAt: '2026-11-01T00:00:00Z',
  status: 'REVIEW_REQUIRED', source: 'MANUAL',
};

it('does not offer a mock decision when the pending API is unavailable', async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: false, status: 404 });
  vi.stubGlobal('fetch', fetchMock);

  const view = render(<ReviewPage />);
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

  await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(3));
  expect(toast.error).toHaveBeenCalledWith('Câu hỏi đã được người khác xử lý. Đang tải lại…');
  expect(toast.success).not.toHaveBeenCalled();
});
