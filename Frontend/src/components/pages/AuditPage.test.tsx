import { cleanup, render, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import AuditPage from './AuditPage';

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

it('does not display or export sample audit records when the API is unavailable', async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: false, status: 404 });
  vi.stubGlobal('fetch', fetchMock);
  const view = render(<AuditPage />);

  await waitFor(() => expect(view.getByText('Không tải được lịch sử thao tác. Vui lòng thử lại.')).toBeDefined());
  expect(view.queryByText('Cấp vai trò Trưởng Bộ Môn (Toán)')).toBeNull();
  expect((view.container.querySelector('#btn-export-audit') as HTMLButtonElement).disabled).toBe(true);
  expect(fetchMock).toHaveBeenCalledWith('/api/v1/audit/role-assignments?pageSize=100', { credentials: 'include' });
});

it('rejects an unknown audit action instead of rendering it', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => [{
    id: 'a1', occurredAt: '2026-10-16T07:30:00Z', actor: 'actor',
    action: 'UNTRUSTED', target: 'target', detail: 'detail', schoolName: 'school',
  }] }));
  const view = render(<AuditPage />);
  await waitFor(() => expect(view.getByText('Không tải được lịch sử thao tác. Vui lòng thử lại.')).toBeDefined());
});

it('shows a scoped role audit response', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => [{
    id: 'a1', occurredAt: '2026-10-16T07:30:00Z', actor: 'school.admin',
    action: 'ROLE_GRANTED', target: 'teacher.1', detail: 'Granted Teacher', schoolName: 'School A',
  }] }));
  const view = render(<AuditPage />);
  await waitFor(() => expect(view.getByText('school.admin')).toBeDefined());
  expect(view.getByText('teacher.1')).toBeDefined();
  expect(view.getByText('School A')).toBeDefined();
  expect((view.container.querySelector('#btn-export-audit') as HTMLButtonElement).disabled).toBe(false);
});
