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

it('opens the revision history from the editor without losing what the author has typed', async () => {
  const detail = {
    id: 'q-1', revision: 2, status: 'APPROVED', origin: 'MANUAL', stem: 'Nội dung cũ', options: ['a', 'b', 'c', 'd'], correctOption: 'A',
    subject: { id: 'sub-a', name: 'Môn A' }, topic: { id: 'top-a', name: 'Chủ đề A' }, difficulty: 'EASY', source: null,
    ownerUserId: 'user-a', updatedAt: '2026-11-01T00:00:00Z', approvedRevision: 2, revisions: [],
  };
  const revision = (n: number) => ({
    revision: n, status: 'APPROVED', origin: 'MANUAL', stem: `Nội dung ${n}`, options: ['a', 'b', 'c', 'd'], correctOption: 'A',
    topic: { id: 'top-a', name: 'Chủ đề A' }, difficulty: 'EASY', source: null, importBatchId: null, importRowNumber: null,
    createdByUserId: 'user-a', createdAt: '2026-11-01T00:00:00Z', isLatest: n === 2, isApprovedInUse: n === 2,
  });
  mocks.apiFetch.mockImplementation((url: string) => {
    if (url === '/api/v1/catalog') return json(200, catalog);
    if (url === '/api/v1/questions/q-1') return json(200, detail);
    if (url === '/api/v1/questions/q-1/revisions') return json(200, { id: 'q-1', subject: detail.subject, latestRevision: 2, approvedRevision: 2, revisions: [revision(2), revision(1)] });
    return json(200, { items: [{ ...detail, subject: detail.subject, topic: detail.topic }], page: 1, pageSize: 20, total: 1 });
  });

  const view = render(<QuestionsPage />);
  fireEvent.click(await view.findByLabelText('Sửa'));
  const stem = await view.findByLabelText(/Nội dung/) as HTMLTextAreaElement;
  fireEvent.change(stem, { target: { value: 'Đang gõ dở' } });

  fireEvent.click(view.container.querySelector('#btn-editor-history')!);
  await view.findByText('Đang dùng để thi');
  fireEvent.click(view.container.querySelector('#btn-close-history')!);

  expect(view.container.querySelector('#btn-save-draft')).not.toBeNull();
  expect((view.container.querySelector('#q-content') as HTMLTextAreaElement).value).toBe('Đang gõ dở');
});
