import { cleanup, fireEvent, render } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import GeneratePage from './GeneratePage';

const mocks = vi.hoisted(() => ({ apiFetch: vi.fn() }));
vi.mock('@/contexts/AuthContext', () => ({ useAuth: () => ({ apiFetch: mocks.apiFetch, csrfToken: 'csrf' }) }));
afterEach(() => { cleanup(); vi.clearAllMocks(); });
const json = (status: number, body: unknown) => Promise.resolve({ ok: status < 300, status, json: async () => body });

it('submits the pinned blueprint with CSRF/idempotency and reports the actual gap', async () => {
  mocks.apiFetch.mockImplementation((url: string) => {
    if (url.startsWith('/api/v1/blueprints')) return json(200, { items: [{ id: 'bp', name: 'Ma trận A', version: 'version' }], total: 1 });
    if (url.endsWith('/generate')) return json(202, { id: 'exam', status: 'QUEUED' });
    return json(200, { id: 'exam', status: 'PARTIAL', items: [], gaps: [{ slotIndex: 0, missingCount: 2, missingPoints: 1 }], job: { status: 'Succeeded' } });
  });
  const view = render(<GeneratePage />);
  await view.findByText('Ma trận A');
  fireEvent.change(view.getByLabelText('Ma trận', { exact: true }), { target: { value: 'bp' } });
  fireEvent.click(view.getByText('Chọn câu đã duyệt'));
  await view.findByText('Chưa đủ câu hỏi');
  const request = mocks.apiFetch.mock.calls.find(([url]) => url.endsWith('/generate'))![1];
  expect(request.headers['X-CSRF-TOKEN']).toBe('csrf');
  expect(request.headers['Idempotency-Key']).toBeTruthy();
  expect(JSON.parse(request.body).blueprintVersion).toBe('version');
  expect(view.queryByText('Đủ câu hỏi — chờ duyệt đề')).toBeNull();
});

it('reports stale blueprint instead of simulating generation', async () => {
  mocks.apiFetch.mockImplementation((url: string) => url.startsWith('/api/v1/blueprints')
    ? json(200, { items: [{ id: 'bp', name: 'Ma trận A', version: 'version' }], total: 1 }) : json(409, {}));
  const view = render(<GeneratePage />);
  await view.findByText('Ma trận A');
  fireEvent.change(view.getByLabelText('Ma trận', { exact: true }), { target: { value: 'bp' } });
  fireEvent.click(view.getByText('Chọn câu đã duyệt'));
  await view.findByText('Ma trận đã thay đổi hoặc được lưu trữ. Hãy tải lại trang để chọn bản mới.');
  expect(mocks.apiFetch.mock.calls.some(([url]) => url.endsWith('/selection'))).toBe(false);
});
