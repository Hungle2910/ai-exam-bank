import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { AuthProvider, useAuth } from './AuthContext';

const userA = { id: 'a', userName: 'admin.a', roles: [{ role: 'SchoolAdmin', schoolId: 'school-a', departmentId: null }] };
const userB = { id: 'b', userName: 'admin.b', roles: [{ role: 'SchoolAdmin', schoolId: 'school-b', departmentId: null }] };

function response(status: number, data?: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => data,
  } as Response;
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>(done => { resolve = done; });
  return { promise, resolve };
}

function Probe() {
  const { user, sessionExpired, login, logout } = useAuth();
  const [error, setError] = useState('');
  return <>
    <span data-testid="user">{user?.userName ?? 'none'}</span>
    <span data-testid="expired">{String(sessionExpired)}</span>
    <span data-testid="error">{error}</span>
    <button onClick={() => void login('admin.b', 'password').catch(e => setError(e.message))}>Login B</button>
    <button onClick={() => void logout().catch(e => setError(e.message))}>Logout</button>
  </>;
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

it('ignores a late successful /auth/me response after logout', async () => {
  const staleMe = deferred<Response>();
  let meCalls = 0;
  vi.stubGlobal('fetch', vi.fn((input: string) => {
    if (input === '/auth/me') return ++meCalls === 1 ? Promise.resolve(response(200, userA)) : staleMe.promise;
    if (input === '/auth/csrf') return Promise.resolve(response(200, { token: 'csrf' }));
    if (input === '/auth/logout') return Promise.resolve(response(204));
    throw new Error(`Unexpected request: ${input}`);
  }));

  render(<AuthProvider><Probe /></AuthProvider>);
  await waitFor(() => expect(screen.getByTestId('user').textContent).toBe('admin.a'));
  act(() => window.dispatchEvent(new Event('focus')));
  await waitFor(() => expect(meCalls).toBe(2));
  fireEvent.click(screen.getByText('Logout'));
  await waitFor(() => expect(screen.getByTestId('user').textContent).toBe('none'));

  await act(async () => { staleMe.resolve(response(200, userA)); await staleMe.promise; });
  expect(screen.getByTestId('user').textContent).toBe('none');
  expect(screen.getByTestId('expired').textContent).toBe('false');
});

it('ignores a late 401 from the previous session after logging in again', async () => {
  const staleMe = deferred<Response>();
  let meCalls = 0;
  vi.stubGlobal('fetch', vi.fn((input: string) => {
    if (input === '/auth/me') {
      meCalls += 1;
      if (meCalls === 1) return Promise.resolve(response(200, userA));
      if (meCalls === 2) return staleMe.promise;
      return Promise.resolve(response(200, userB));
    }
    if (input === '/auth/csrf') return Promise.resolve(response(200, { token: 'csrf' }));
    if (input === '/auth/logout') return Promise.resolve(response(204));
    if (input === '/auth/login') return Promise.resolve(response(204));
    throw new Error(`Unexpected request: ${input}`);
  }));

  render(<AuthProvider><Probe /></AuthProvider>);
  await waitFor(() => expect(screen.getByTestId('user').textContent).toBe('admin.a'));
  act(() => window.dispatchEvent(new Event('focus')));
  await waitFor(() => expect(meCalls).toBe(2));
  fireEvent.click(screen.getByText('Logout'));
  await waitFor(() => expect(screen.getByTestId('user').textContent).toBe('none'));
  fireEvent.click(screen.getByText('Login B'));
  await waitFor(() => expect(screen.getByTestId('user').textContent).toBe('admin.b'));

  await act(async () => { staleMe.resolve(response(401)); await staleMe.promise; });
  expect(screen.getByTestId('user').textContent).toBe('admin.b');
  expect(screen.getByTestId('expired').textContent).toBe('false');
});

it('expires the session when a concurrent 401 follows a successful /auth/me check', async () => {
  const firstCheck = deferred<Response>();
  const secondCheck = deferred<Response>();
  let meCalls = 0;
  vi.stubGlobal('fetch', vi.fn((input: string) => {
    if (input === '/auth/me') {
      meCalls += 1;
      if (meCalls === 1) return Promise.resolve(response(200, userA));
      return meCalls === 2 ? firstCheck.promise : secondCheck.promise;
    }
    if (input === '/auth/csrf') return Promise.resolve(response(200, { token: 'csrf' }));
    throw new Error(`Unexpected request: ${input}`);
  }));

  render(<AuthProvider><Probe /></AuthProvider>);
  await waitFor(() => expect(screen.getByTestId('user').textContent).toBe('admin.a'));
  act(() => {
    window.dispatchEvent(new Event('focus'));
    window.dispatchEvent(new Event('focus'));
  });
  await waitFor(() => expect(meCalls).toBe(3));

  await act(async () => { firstCheck.resolve(response(200, userA)); await firstCheck.promise; });
  await act(async () => { secondCheck.resolve(response(401)); await secondCheck.promise; });
  expect(screen.getByTestId('user').textContent).toBe('none');
  expect(screen.getByTestId('expired').textContent).toBe('true');
});

it('keeps the authenticated user when logout fails', async () => {
  vi.stubGlobal('fetch', vi.fn((input: string) => {
    if (input === '/auth/me') return Promise.resolve(response(200, userA));
    if (input === '/auth/csrf') return Promise.resolve(response(200, { token: 'csrf' }));
    if (input === '/auth/logout') return Promise.resolve(response(400));
    throw new Error(`Unexpected request: ${input}`);
  }));

  render(<AuthProvider><Probe /></AuthProvider>);
  await waitFor(() => expect(screen.getByTestId('user').textContent).toBe('admin.a'));
  fireEvent.click(screen.getByText('Logout'));
  await waitFor(() => expect(screen.getByTestId('error').textContent).toBe('Đăng xuất thất bại'));
  expect(screen.getByTestId('user').textContent).toBe('admin.a');
  expect(screen.getByTestId('expired').textContent).toBe('false');
});
