import { cleanup, fireEvent, render, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import BlueprintsPage from './BlueprintsPage';

const mocks = vi.hoisted(() => ({ apiFetch: vi.fn() }));
vi.mock('@/contexts/AuthContext', () => ({ useAuth: () => ({ apiFetch: mocks.apiFetch, csrfToken: 'test-csrf' }) }));
afterEach(() => { cleanup(); vi.clearAllMocks(); vi.restoreAllMocks(); });
const response = (status: number, data: unknown) => Promise.resolve({ ok: status < 300, status, json: async () => data });
const catalog = [{ id: 'math', name: 'Toán', canWrite: true, topics: [{ id: 'topic', name: 'Đạo hàm' }] }];
const blueprint = { id: 'bp', name: 'Đề A', subjectId: 'math', durationMinutes: 45, totalQuestions: 1, totalPoints: 1, version: 'old', slots: [{ topicId: 'topic', difficulty: 'EASY', questionCount: 1, pointsPerQuestion: 1 }] };

it('keeps unsaved edits and the old version on conflict', async () => {
  mocks.apiFetch.mockImplementation((url: string, init?: RequestInit) => {
    if (init?.method === 'PUT') return response(409, {});
    return response(200, url === '/api/v1/catalog' ? catalog : { items: [blueprint], total: 1 });
  });
  const view = render(<BlueprintsPage />);
  fireEvent.click(await view.findByText('Mở Đề A'));
  fireEvent.change(view.getByLabelText('Tên ma trận'), { target: { value: 'Nội dung chưa lưu' } });
  fireEvent.click(view.getByText('Lưu ma trận'));
  await view.findByText('Tải phiên bản mới');
  expect((view.getByLabelText('Tên ma trận') as HTMLInputElement).value).toBe('Nội dung chưa lưu');
  const save = mocks.apiFetch.mock.calls.find(([, init]) => init?.method === 'PUT');
  expect(JSON.parse(save![1].body).version).toBe('old');
  expect(save![1].headers['X-CSRF-TOKEN']).toBe('test-csrf');
});

it('shows server field errors without claiming a successful save', async () => {
  mocks.apiFetch.mockImplementation((url: string, init?: RequestInit) => {
    if (init?.method === 'PUT') return response(400, { errors: { totalPoints: ['Tổng điểm sai'] } });
    return response(200, url === '/api/v1/catalog' ? catalog : { items: [blueprint], total: 1 });
  });
  const view = render(<BlueprintsPage />);
  fireEvent.click(await view.findByText('Mở Đề A'));
  fireEvent.click(view.getByText('Lưu ma trận'));
  await view.findByText('Tổng điểm sai');
  expect(view.queryByText('Đã lưu ma trận trên máy chủ.')).toBeNull();
});

it('reports list failure and never substitutes demo blueprints', async () => {
  mocks.apiFetch.mockImplementation(() => response(500, {}));
  const view = render(<BlueprintsPage />);
  await waitFor(() => expect(view.getByRole('alert')).toBeTruthy());
  expect(view.queryByText('Đề A')).toBeNull();
});

it('archives with the displayed version and removes the item only after server success', async () => {
  vi.spyOn(window, 'confirm').mockReturnValue(true);
  let archived = false;
  mocks.apiFetch.mockImplementation((url: string, init?: RequestInit) => {
    if (url.endsWith('/archive') && init?.method === 'POST') { archived = true; return response(200, { ...blueprint, archivedAt: '2026-11-07T00:00:00Z' }); }
    return response(200, url === '/api/v1/catalog' ? catalog : { items: archived ? [] : [blueprint], total: archived ? 0 : 1 });
  });
  const view = render(<BlueprintsPage />);
  fireEvent.click(await view.findByText('Lưu trữ Đề A'));
  await waitFor(() => expect(view.queryByText('Mở Đề A')).toBeNull());
  const request = mocks.apiFetch.mock.calls.find(([url]) => url.endsWith('/archive'));
  expect(JSON.parse(request![1].body)).toEqual({ version: 'old' });
  expect(request![1].headers['X-CSRF-TOKEN']).toBe('test-csrf');
});

it('opens archived content read-only', async () => {
  mocks.apiFetch.mockImplementation((url: string) => response(200, url === '/api/v1/catalog' ? catalog : {
    items: [{ ...blueprint, archivedAt: '2026-11-07T00:00:00Z' }], total: 1,
  }));
  const view = render(<BlueprintsPage />);
  fireEvent.click(await view.findByText('Mở Đề A'));
  expect((view.getByText('Lưu ma trận') as HTMLButtonElement).disabled).toBe(true);
  expect(view.getByLabelText('Tên ma trận').closest('fieldset')?.disabled).toBe(true);
  expect((view.getByText('Danh sách') as HTMLButtonElement).disabled).toBe(false);
});
