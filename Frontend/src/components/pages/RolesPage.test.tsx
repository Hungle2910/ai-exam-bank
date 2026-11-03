import { act, cleanup, fireEvent, render, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import RolesPage from './RolesPage';

const mocks = vi.hoisted(() => ({
  apiFetch: vi.fn(),
  success: vi.fn(),
  error: vi.fn(),
}));

vi.mock('@/contexts/AuthContext', () => ({
  useAuth: () => ({ apiFetch: mocks.apiFetch, csrfToken: 'csrf', primaryRole: 'SchoolAdmin' }),
}));

vi.mock('@/contexts/ToastContext', () => ({
  useToast: () => ({ success: mocks.success, error: mocks.error }),
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

it('selects the SchoolAdmin school when the list arrives after the grant modal opens', async () => {
  let resolveSchools!: (value: { ok: boolean; json: () => Promise<unknown> }) => void;
  const schoolsResponse = new Promise<{ ok: boolean; json: () => Promise<unknown> }>(resolve => {
    resolveSchools = resolve;
  });
  mocks.apiFetch.mockImplementation((url: string) => {
    if (url === '/api/v1/admin/schools') return schoolsResponse;
    if (url === '/api/v1/admin/schools/school-a/role-assignments') {
      return Promise.resolve({ ok: true, json: async () => [] });
    }
    if (url === '/api/v1/admin/schools/school-a/departments') {
      return Promise.resolve({ ok: true, json: async () => [{ id: 'dept-a', schoolId: 'school-a', name: 'Math' }] });
    }
    if (url === '/api/v1/admin/users/user-a/role-assignments') {
      return Promise.resolve({ status: 201 });
    }
    throw new Error(`Unexpected request: ${url}`);
  });

  const view = render(<RolesPage />);
  fireEvent.click(view.getByText('Gán quyền'));
  const schoolSelect = view.container.querySelector('#grant-school') as HTMLSelectElement;
  expect(schoolSelect.value).toBe('');

  resolveSchools({ ok: true, json: async () => [{ id: 'school-a', name: 'School A' }] });
  await waitFor(() => expect(schoolSelect.value).toBe('school-a'));
  await waitFor(() => expect((view.container.querySelector('#grant-department') as HTMLSelectElement).options.length).toBe(2));

  fireEvent.change(view.container.querySelector('#grant-userid') as HTMLInputElement, { target: { value: 'user-a' } });
  fireEvent.change(view.container.querySelector('#grant-department') as HTMLSelectElement, { target: { value: 'dept-a' } });
  fireEvent.click(view.container.querySelector('#btn-submit-grant') as HTMLButtonElement);

  await waitFor(() => {
    const call = mocks.apiFetch.mock.calls.find(([url]) => url === '/api/v1/admin/users/user-a/role-assignments');
    expect(call).toBeDefined();
    expect(JSON.parse(call![1].body)).toMatchObject({ role: 'DepartmentHead', schoolId: 'school-a', departmentId: 'dept-a' });
  });
});

it('keeps the selected school assignments when an older school responds later', async () => {
  let resolveA!: (value: { ok: boolean; json: () => Promise<unknown> }) => void;
  const schoolA = new Promise<{ ok: boolean; json: () => Promise<unknown> }>(resolve => { resolveA = resolve; });
  mocks.apiFetch.mockImplementation((url: string) => {
    if (url === '/api/v1/admin/schools') {
      return Promise.resolve({ ok: true, json: async () => [
        { id: 'school-a', name: 'School A' }, { id: 'school-b', name: 'School B' },
      ] });
    }
    if (url === '/api/v1/admin/schools/school-a/role-assignments') return schoolA;
    if (url === '/api/v1/admin/schools/school-b/role-assignments') {
      return Promise.resolve({ ok: true, json: async () => [
        { id: 'assignment-b', userId: 'user-b', role: 'Teacher', schoolId: 'school-b', departmentId: null },
      ] });
    }
    throw new Error(`Unexpected request: ${url}`);
  });

  const view = render(<RolesPage />);
  await waitFor(() => expect(mocks.apiFetch).toHaveBeenCalledWith('/api/v1/admin/schools/school-a/role-assignments'));
  fireEvent.change(view.container.querySelector('#roles-school-selector') as HTMLSelectElement, { target: { value: 'school-b' } });
  await waitFor(() => expect(view.getByText(/user-b/)).toBeDefined());

  await act(async () => {
    resolveA({ ok: true, json: async () => [
      { id: 'assignment-a', userId: 'user-a', role: 'Teacher', schoolId: 'school-a', departmentId: null },
    ] });
    await schoolA;
  });
  expect(view.getByText(/user-b/)).toBeDefined();
  expect(view.queryByText(/user-a/)).toBeNull();
});
