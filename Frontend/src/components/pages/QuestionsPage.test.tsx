import { cleanup, fireEvent, render, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import QuestionsPage from './QuestionsPage';

const mocks = vi.hoisted(() => ({
  apiFetch: vi.fn(),
  success: vi.fn(),
}));

vi.mock('@/contexts/AuthContext', () => ({
  useAuth: () => ({ apiFetch: mocks.apiFetch, csrfToken: 'csrf', user: { id: 'user-a' } }),
}));

vi.mock('@/contexts/ToastContext', () => ({
  useToast: () => ({ success: mocks.success }),
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const json = (status: number, body: unknown = null) => Promise.resolve({ ok: status >= 200 && status < 300, status, json: async () => body });
const emptyList = { items: [], page: 1, pageSize: 20, total: 0 };
const catalog = [{ id: 'sub-a', code: 'A', name: 'Môn A', canWrite: true, topics: [{ id: 'top-a', code: 'A.1', name: 'Chủ đề A' }] }];

it('loads the question list and the catalog through apiFetch so a 401 ends the session', async () => {
  mocks.apiFetch.mockImplementation((url: string) => json(url === '/api/v1/catalog' ? 200 : 401, url === '/api/v1/catalog' ? catalog : null));

  const view = render(<QuestionsPage />);

  await waitFor(() => expect(view.getByText('Phiên đăng nhập đã hết hạn, hãy đăng nhập lại')).toBeTruthy());
  const urls = mocks.apiFetch.mock.calls.map(([url]) => url as string);
  expect(urls).toContain('/api/v1/catalog');
  expect(urls.some(u => u.startsWith('/api/v1/questions?'))).toBe(true);
});

it('keeps a retry for the catalog and restores subject choices once it loads', async () => {
  let catalogCalls = 0;
  mocks.apiFetch.mockImplementation((url: string) => {
    if (url === '/api/v1/catalog') return ++catalogCalls === 1 ? json(500) : json(200, catalog);
    return json(200, emptyList);
  });

  const view = render(<QuestionsPage />);
  const retry = await view.findByText('Thử lại', { selector: '#btn-retry-catalog' });
  expect(view.container.querySelector('#btn-create-question')).toBeNull();

  fireEvent.click(retry);

  await waitFor(() => expect(view.container.querySelector('#btn-create-question')).not.toBeNull());
  expect(view.container.querySelector('#btn-retry-catalog')).toBeNull();
  expect(Array.from((view.container.querySelector('#filter-subject') as HTMLSelectElement).options).map(o => o.text)).toContain('Môn A');
});
