import { cleanup, fireEvent, render, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import ReviewPage from './ReviewPage';

vi.mock('@/contexts/AuthContext', () => ({
  useAuth: () => ({ csrfToken: 'test-csrf' }),
}));

vi.mock('@/contexts/ToastContext', () => ({
  useToast: () => ({ success: vi.fn(), error: vi.fn() }),
}));

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

it('does not show mock review decisions when the review API is unavailable', async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: false, status: 404 });
  vi.stubGlobal('fetch', fetchMock);

  const view = render(<ReviewPage />);

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
});
