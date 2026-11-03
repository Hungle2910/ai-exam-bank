'use client';

import React, { useState } from 'react';
import { Eye, EyeOff, BookOpen, LogIn, AlertCircle } from 'lucide-react';
import { useAuth } from '@/contexts/AuthContext';
import clsx from 'clsx';

/* ============================================================
   LOGIN PAGE
   ============================================================ */

export default function LoginPage() {
  const { login, sessionExpired } = useAuth();
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [showPwd, setShowPwd] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!userName.trim() || !password) {
      setError('Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu');
      return;
    }
    setLoading(true);
    setError('');
    try {
      await login(userName.trim(), password);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Đăng nhập thất bại');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-page">
      {/* Background decorations */}
      <div className="login-bg-decoration" style={{ width: 600, height: 600, top: -200, right: -200 }} />
      <div className="login-bg-decoration" style={{ width: 400, height: 400, bottom: -150, left: -150 }} />

      <div className="login-card">
        {/* Logo */}
        <div className="login-logo">
          <div className="login-logo-icon">
            <BookOpen size={22} color="white" />
          </div>
          <div className="login-logo-text">
            <h1>AI Exam Bank</h1>
            <p>Hệ thống ngân hàng câu hỏi thông minh</p>
          </div>
        </div>

        {/* Heading */}
        <div className="login-heading">
          <h2>Đăng nhập</h2>
          <p>Chào mừng trở lại! Vui lòng nhập thông tin tài khoản.</p>
        </div>

        {/* Form */}
        <form className="login-form" onSubmit={handleSubmit} noValidate>
          {/* Error Alert */}
          {(error || sessionExpired) && (
            <div style={{
              display: 'flex',
              alignItems: 'flex-start',
              gap: 10,
              background: 'var(--danger-50)',
              border: '1px solid #fecaca',
              borderRadius: 'var(--radius-md)',
              padding: '12px 14px',
              marginBottom: 16,
              animation: 'fade-in 0.2s ease',
            }}>
              <AlertCircle size={16} style={{ color: 'var(--danger-500)', marginTop: 1, flexShrink: 0 }} />
              <span style={{ fontSize: 13, color: 'var(--danger-700)', fontWeight: 500 }}>
                {error || 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'}
              </span>
            </div>
          )}

          {/* Username */}
          <div className="form-group">
            <label className="form-label" htmlFor="login-username">
              Tên đăng nhập
            </label>
            <div className="input-group">
              <svg className="input-icon-left" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>
              </svg>
              <input
                id="login-username"
                type="text"
                className={clsx('form-input with-icon-left', error && 'error')}
                placeholder="Nhập tên đăng nhập"
                value={userName}
                onChange={e => { setUserName(e.target.value); setError(''); }}
                autoComplete="username"
                autoFocus
                disabled={loading}
              />
            </div>
          </div>

          {/* Password */}
          <div className="form-group">
            <label className="form-label" htmlFor="login-password">
              Mật khẩu
            </label>
            <div className="input-group" style={{ position: 'relative' }}>
              <svg className="input-icon-left" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect width="18" height="11" x="3" y="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/>
              </svg>
              <input
                id="login-password"
                type={showPwd ? 'text' : 'password'}
                className={clsx('form-input with-icon-left', error && 'error')}
                placeholder="Nhập mật khẩu"
                value={password}
                onChange={e => { setPassword(e.target.value); setError(''); }}
                autoComplete="current-password"
                disabled={loading}
                style={{ paddingRight: 38 }}
              />
              <button
                type="button"
                onClick={() => setShowPwd(v => !v)}
                aria-label={showPwd ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                id="btn-toggle-password"
                style={{
                  position: 'absolute', right: 10, top: '50%', transform: 'translateY(-50%)',
                  background: 'none', border: 'none', color: 'var(--gray-400)', cursor: 'pointer',
                  padding: 4, display: 'flex', alignItems: 'center',
                }}
              >
                {showPwd ? <EyeOff size={15} /> : <Eye size={15} />}
              </button>
            </div>
          </div>

          {/* Submit */}
          <button
            type="submit"
            id="btn-login"
            className={clsx('btn btn-primary', 'w-full', loading && 'btn-loading')}
            disabled={loading}
            style={{ marginTop: 8, height: 40, fontSize: 14 }}
          >
            {!loading && <LogIn size={15} />}
            {loading ? 'Đang đăng nhập…' : 'Đăng nhập'}
          </button>
        </form>

        {/* Footer */}
        <div className="login-footer">
          <p>Quên mật khẩu? Liên hệ <strong>quản trị viên trường</strong> của bạn.</p>
          <p style={{ marginTop: 8, color: 'var(--gray-300)' }}>
            AI Exam Bank · AWS FCAJ 2026 · AWS Hello World
          </p>
        </div>
      </div>
    </div>
  );
}
