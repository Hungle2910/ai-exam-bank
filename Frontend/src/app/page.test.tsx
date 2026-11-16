import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import Home from './page';

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

it('returns to login with an expiry message after the active session becomes unauthorized', async () => {
  let meCalls = 0;
  vi.stubGlobal('fetch', vi.fn((input: string) => {
    if (input === '/auth/me') {
      meCalls += 1;
      return Promise.resolve(new Response(
        meCalls === 1
          ? JSON.stringify({ id: 'user-a', userName: 'school.admin.a', roles: [{ role: 'SchoolAdmin', schoolId: 'school-a', departmentId: null }] })
          : null,
        { status: meCalls === 1 ? 200 : 401, headers: { 'Content-Type': 'application/json' } },
      ));
    }
    if (input === '/auth/csrf') {
      return Promise.resolve(Response.json({ token: 'csrf-token' }));
    }
    throw new Error(`Unexpected request: ${input}`);
  }));

  render(<Home />);
  await waitFor(() => expect(screen.getAllByText(/school\.admin\.a/).length).toBeGreaterThan(0));

  act(() => window.dispatchEvent(new Event('focus')));

  await waitFor(() => expect(screen.getByText('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.')).toBeTruthy());
  expect(screen.getByLabelText('Tên đăng nhập')).toBeTruthy();
});
