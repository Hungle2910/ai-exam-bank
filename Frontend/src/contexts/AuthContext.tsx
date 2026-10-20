'use client';

import React, { createContext, useContext, useState, useCallback, useEffect } from 'react';

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
  login: (userName: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refetch: () => Promise<void>;
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

  const fetchCsrf = useCallback(async () => {
    try {
      const res = await fetch('/auth/csrf', { credentials: 'include' });
      if (res.ok) {
        const data = await res.json();
        setCsrfToken(data.token);
        return data.token as string;
      }
    } catch {
      // ignore
    }
    return null;
  }, []);

  const fetchMe = useCallback(async () => {
    try {
      const res = await fetch('/auth/me', { credentials: 'include' });
      if (res.ok) {
        const data = await res.json();
        setUser(data);
      } else {
        setUser(null);
      }
    } catch {
      setUser(null);
    }
  }, []);

  const refetch = useCallback(async () => {
    await Promise.all([fetchMe(), fetchCsrf()]);
  }, [fetchMe, fetchCsrf]);

  useEffect(() => {
    const init = async () => {
      await Promise.all([fetchMe(), fetchCsrf()]);
      setLoading(false);
    };
    init();
  }, [fetchMe, fetchCsrf]);

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

    await fetchMe();
    await fetchCsrf(); // rotate token
  }, [csrfToken, fetchCsrf, fetchMe]);

  const logout = useCallback(async () => {
    if (!csrfToken) return;
    try {
      await fetch('/auth/logout', {
        method: 'POST',
        credentials: 'include',
        headers: { 'X-CSRF-TOKEN': csrfToken },
      });
    } finally {
      setUser(null);
      await fetchCsrf();
    }
  }, [csrfToken, fetchCsrf]);

  // Determine primary role (highest privilege)
  const ROLE_PRIORITY: AppRole[] = ['MinistryAdmin', 'SchoolAdmin', 'DepartmentHead', 'Teacher'];
  const primaryRole: AppRole | null = user
    ? ROLE_PRIORITY.find(r => user.roles.some(ra => ra.role === r)) ?? null
    : null;

  return (
    <AuthContext.Provider value={{ user, csrfToken, loading, login, logout, refetch, primaryRole }}>
      {children}
    </AuthContext.Provider>
  );
}
