'use client';

import React, { createContext, useContext, useState, useCallback, useEffect, useRef } from 'react';

/* ============================================================
   TYPES
   ============================================================ */

export type AppRole = 'MinistryAdmin' | 'SchoolAdmin' | 'DepartmentHead' | 'Teacher';

export interface RoleAssignment {
  role: AppRole;
  schoolId: string | null;
  departmentId: string | null;
}

export interface AuthUser {
  id: string;
  userName: string;
  roles: RoleAssignment[];
}

export interface AuthContextType {
  user: AuthUser | null;
  csrfToken: string | null;
  loading: boolean;
  sessionExpired: boolean;
  login: (userName: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refetch: () => Promise<void>;
  apiFetch: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;
  primaryRole: AppRole | null;
}

/* ============================================================
   CONTEXT
   ============================================================ */

const AuthContext = createContext<AuthContextType | null>(null);

export function useAuth(): AuthContextType {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}

/* ============================================================
   PROVIDER
   ============================================================ */

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [csrfToken, setCsrfToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [sessionExpired, setSessionExpired] = useState(false);
  const authenticated = useRef(false);
  const authVersion = useRef(0);

  const fetchCsrf = useCallback(async () => {
    const version = authVersion.current;
    try {
      const res = await fetch('/auth/csrf', { credentials: 'include', cache: 'no-store' });
      if (res.ok) {
        const data = await res.json();
        if (version !== authVersion.current) return null;
        setCsrfToken(data.token);
        return data.token as string;
      }
    } catch {
      // ignore
    }
    return null;
  }, []);

  const fetchMe = useCallback(async () => {
    const version = authVersion.current;
    try {
      const res = await fetch('/auth/me', { credentials: 'include', cache: 'no-store' });
      if (version !== authVersion.current) return;
      if (res.ok) {
        const data = await res.json();
        if (version !== authVersion.current) return;
        authenticated.current = true;
        setUser(data);
      } else if (res.status === 401) {
        if (authenticated.current) {
          authVersion.current += 1;
          setSessionExpired(true);
        }
        authenticated.current = false;
        setUser(null);
      }
    } catch {
      // Keep the current screen during a temporary connection failure.
    }
  }, []);

  const apiFetch = useCallback(async (input: RequestInfo | URL, init?: RequestInit) => {
    const version = authVersion.current;
    const res = await fetch(input, { credentials: 'include', cache: 'no-store', ...init });
    if (res.status === 401 && version === authVersion.current && authenticated.current) {
      authVersion.current += 1;
      authenticated.current = false;
      setSessionExpired(true);
      setUser(null);
    }
    return res;
  }, []);

  const refetch = useCallback(async () => {
    await fetchMe();
    await fetchCsrf();
  }, [fetchMe, fetchCsrf]);

  useEffect(() => {
    const init = async () => {
      await fetchMe();
      await fetchCsrf();
      setLoading(false);
    };
    init();
  }, [fetchMe, fetchCsrf]);

  useEffect(() => {
    const checkOnReturn = () => {
      if (authenticated.current && document.visibilityState === 'visible') void fetchMe();
    };
    window.addEventListener('focus', checkOnReturn);
    document.addEventListener('visibilitychange', checkOnReturn);
    return () => {
      window.removeEventListener('focus', checkOnReturn);
      document.removeEventListener('visibilitychange', checkOnReturn);
    };
  }, [fetchMe]);

  const login = useCallback(async (userName: string, password: string) => {
    const token = csrfToken ?? await fetchCsrf();
    if (!token) throw new Error('Could not obtain CSRF token');

    const res = await fetch('/auth/login', {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': token,
      },
      body: JSON.stringify({ userName, password }),
    });

    if (res.status === 401) throw new Error('Tên đăng nhập hoặc mật khẩu không đúng');
    if (res.status === 429) throw new Error('Quá nhiều lần thử. Vui lòng đợi 1 phút rồi thử lại');
    if (!res.ok) throw new Error('Đăng nhập thất bại. Vui lòng thử lại');

    authVersion.current += 1;
    await fetchMe();
    await fetchCsrf(); // rotate token
    setSessionExpired(false);
  }, [csrfToken, fetchCsrf, fetchMe]);

  const logout = useCallback(async () => {
    const token = csrfToken ?? await fetchCsrf();
    if (!token) throw new Error('Không thể lấy CSRF token');

    const res = await fetch('/auth/logout', {
      method: 'POST',
      credentials: 'include',
      headers: { 'X-CSRF-TOKEN': token },
    });
    if (!res.ok) throw new Error('Đăng xuất thất bại');

    authVersion.current += 1;
    authenticated.current = false;
    setUser(null);
    setSessionExpired(false);
    setCsrfToken(null);
    await fetchCsrf();
  }, [csrfToken, fetchCsrf]);

  // Determine primary role (highest privilege)
  const ROLE_PRIORITY: AppRole[] = ['MinistryAdmin', 'SchoolAdmin', 'DepartmentHead', 'Teacher'];
  const primaryRole: AppRole | null = user
    ? ROLE_PRIORITY.find(r => user.roles.some(ra => ra.role === r)) ?? null
    : null;

  return (
    <AuthContext.Provider value={{ user, csrfToken, loading, sessionExpired, login, logout, refetch, apiFetch, primaryRole }}>
      {children}
    </AuthContext.Provider>
  );
}
