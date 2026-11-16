'use client';

import React, { useState } from 'react';
import { Eye, EyeOff, LogIn, AlertCircle, Lock } from 'lucide-react';
import { useAuth, AppRole } from '@/contexts/AuthContext';
import clsx from 'clsx';

export default function LoginPage() {
  const { login, loginDemoRole, sessionExpired } = useAuth();
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

  const quickDemoRoles: Array<{ role: AppRole; label: string; sub: string; user: string }> = [
    { role: 'Teacher', label: 'Giáo viên bộ môn', sub: 'Soạn ma trận & rà soát gợi ý AI', user: 'gv.nguyen.van.a' },
    { role: 'DepartmentHead', label: 'Trưởng Bộ môn', sub: 'Duyệt câu hỏi & Xét chọn đề', user: 'tbm.toan.thpt.a' },
    { role: 'SchoolAdmin', label: 'Quản trị Trường', sub: 'Tổ bộ môn, tài khoản & phân quyền', user: 'admin.thpt.a' },
    { role: 'MinistryAdmin', label: 'Quản trị Bộ GD&ĐT', sub: 'Đơn vị trường & Kỳ thi cấp Bộ', user: 'demo.user' },
  ];

  return (
    <div className="login-page">
      <div
        className="login-split-shell"
        style={{
          display: 'grid',
          gridTemplateColumns: '1.1fr 1fr',
          maxWidth: 860,
          width: '100%',
          background: 'var(--paper-sheet)',
          border: '1px solid var(--rule-strong)',
          borderRadius: 4,
          overflow: 'hidden',
        }}
      >
        {/* Left Column: Paper Cover Sheet of the Exam Authoring Desk */}
        <div
          className="login-editorial-col"
          style={{
            background: '#EFECE1',
            borderRight: '1px solid var(--rule-strong)',
            color: 'var(--ink-900)',
            padding: '32px 28px',
            display: 'flex',
            flexDirection: 'column',
            justifyContent: 'space-between',
          }}
        >
          <div>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 10,
                paddingBottom: 12,
                marginBottom: 20,
                borderBottom: '3px double var(--ink-900)',
              }}
            >
              <div
                style={{
                  width: 30,
                  height: 30,
                  borderRadius: 3,
                  background: 'var(--brand-600)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: '#FCFBF7',
                }}
              >
                <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
                  <rect x="2.5" y="1.5" width="11" height="13" rx="1" stroke="currentColor" strokeWidth="1.4" />
                  <line x1="5" y1="5" x2="11" y2="5" stroke="currentColor" strokeWidth="1.4" />
                  <line x1="5" y1="8" x2="11" y2="8" stroke="currentColor" strokeWidth="1.4" />
                  <line x1="5" y1="11" x2="8.5" y2="11" stroke="currentColor" strokeWidth="1.4" />
                </svg>
              </div>
              <div>
                <div style={{ fontFamily: 'var(--font-serif)', fontSize: 15, fontWeight: 700 }}>
                  SỔ BÀN SOẠN ĐỀ &amp; THẨM ĐỊNH
                </div>
                <div style={{ fontSize: 11.5, color: 'var(--ink-600)' }}>
                  Ngân hàng Câu hỏi &amp; Ma trận Đề thi Học vụ
                </div>
              </div>
            </div>

            <h1
              style={{
                fontFamily: 'var(--font-serif)',
                fontSize: 21,
                fontWeight: 700,
                lineHeight: 1.35,
                marginBottom: 12,
                color: 'var(--ink-900)',
              }}
            >
              Soạn đề theo ma trận, rà soát câu hỏi AI gợi ý và duyệt đề trước khi in
            </h1>
            <p style={{ fontSize: 13, color: 'var(--ink-700)', lineHeight: 1.6, marginBottom: 18 }}>
              Mỗi ô trong ma trận được đối chiếu trực tiếp với các phiên bản câu hỏi đã duyệt; câu hỏi gợi ý từ học liệu luôn đi qua bàn rà soát của giáo viên và bút phê của Trưởng bộ môn.
            </p>

            <div style={{ display: 'grid', gap: 10 }}>
              <div className="margin-note green-ink">
                <strong>1. Bàn ma trận 2 chiều:</strong> Theo dõi số câu thực tế (VD: 3/5 câu), đánh dấu góc đỏ tại các ô còn thiếu.
              </div>
              <div className="margin-note">
                <strong>2. Bản nháp AI có trích dẫn SGK:</strong> Đặt câu hỏi cạnh nguồn tham chiếu để giáo viên chỉnh sửa trước khi gửi duyệt.
              </div>
              <div className="margin-note red-ink">
                <strong>3. Bàn duyệt có lề bút phê:</strong> Gắn lý do trả về sát phiên bản liên quan và niêm phong tờ đề chốt khổ A4.
              </div>
            </div>
          </div>

          <div
            style={{
              marginTop: 24,
              paddingTop: 10,
              borderTop: '1px solid var(--rule-base)',
              display: 'flex',
              justifyContent: 'space-between',
              fontSize: 11.5,
              color: 'var(--ink-500)',
            }}
          >
            <span>AWS FCAJ 2026 · AWS Hello World</span>
            <span className="mono-num">M1–M5 Integrated</span>
          </div>
        </div>

        {/* Right Form Column */}
        <div style={{ padding: '32px 28px', display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
          <div className="login-heading">
            <h2>Mở phiên làm việc</h2>
            <p>Nhập tài khoản giáo viên hoặc cán bộ quản lý học vụ.</p>
          </div>

          <form className="login-form" onSubmit={handleSubmit} noValidate>
            {(error || sessionExpired) && (
              <div
                role="alert"
                className="margin-note red-ink"
                style={{
                  display: 'flex',
                  alignItems: 'flex-start',
                  gap: 8,
                  marginBottom: 14,
                }}
              >
                <AlertCircle size={15} style={{ marginTop: 1, flexShrink: 0 }} />
                <span style={{ fontWeight: 600 }}>
                  {error || 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'}
                </span>
              </div>
            )}

            <div className="form-group">
              <label className="form-label" htmlFor="login-username">
                Tên đăng nhập
              </label>
              <input
                id="login-username"
                type="text"
                className={clsx('form-input', error && 'error')}
                placeholder="VD: demo.user hoặc gv.nguyen.van.a"
                value={userName}
                onChange={e => { setUserName(e.target.value); setError(''); }}
                autoComplete="username"
                autoFocus
                disabled={loading}
              />
            </div>

            <div className="form-group">
              <label className="form-label" htmlFor="login-password">
                Mật khẩu
              </label>
              <div className="input-group" style={{ position: 'relative' }}>
                <input
                  id="login-password"
                  type={showPwd ? 'text' : 'password'}
                  className={clsx('form-input', error && 'error')}
                  placeholder="Nhập mật khẩu"
                  value={password}
                  onChange={e => { setPassword(e.target.value); setError(''); }}
                  autoComplete="current-password"
                  disabled={loading}
                  style={{ paddingRight: 36 }}
                />
                <button
                  type="button"
                  onClick={() => setShowPwd(v => !v)}
                  aria-label={showPwd ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                  id="btn-toggle-password"
                  style={{
                    position: 'absolute', right: 8, top: '50%', transform: 'translateY(-50%)',
                    background: 'none', border: 'none', color: 'var(--ink-500)', cursor: 'pointer',
                    padding: 4, display: 'flex', alignItems: 'center',
                  }}
                >
                  {showPwd ? <EyeOff size={14} /> : <Eye size={14} />}
                </button>
              </div>
            </div>

            <button
              type="submit"
              id="btn-login"
              className={clsx('btn btn-primary', 'w-full', loading && 'btn-loading')}
              disabled={loading}
              style={{ marginTop: 4, height: 35 }}
            >
              {!loading && <LogIn size={14} />}
              {loading ? 'Đang xác thực…' : 'Đăng nhập vào bàn soạn đề'}
            </button>
          </form>

          {loginDemoRole && (
            <div style={{ marginTop: 18, paddingTop: 14, borderTop: '1px solid var(--rule-base)' }}>
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  fontSize: 11.5,
                  fontWeight: 700,
                  color: 'var(--ink-600)',
                  marginBottom: 8,
                }}
              >
                <span>Mở nhanh bàn làm việc theo vai trò mẫu</span>
                <span className="badge badge-gray" style={{ fontSize: 10 }}>Mô phỏng cục bộ</span>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
                {quickDemoRoles.map(item => (
                  <button
                    key={item.role}
                    type="button"
                    id={`btn-demo-role-${item.role}`}
                    onClick={() => loginDemoRole(item.role, item.user)}
                    style={{
                      textAlign: 'left',
                      padding: '6px 9px',
                      borderRadius: 3,
                      border: '1px solid var(--rule-base)',
                      background: '#F5F2E8',
                      cursor: 'pointer',
                    }}
                  >
                    <div style={{ fontSize: 12.5, fontWeight: 700, color: 'var(--ink-900)' }}>{item.label}</div>
                    <div style={{ fontSize: 11, color: 'var(--ink-600)', fontFamily: 'var(--font-mono)' }}>
                      {item.user}
                    </div>
                  </button>
                ))}
              </div>
            </div>
          )}

          <div className="login-footer" style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6 }}>
            <Lock size={11} />
            <span>Phiên bảo mật HttpOnly Cookie &amp; CSRF · Tài khoản seed: <code>demo.user</code></span>
          </div>
        </div>
      </div>

      <style>{`
        @media (max-width: 780px) {
          .login-split-shell { grid-template-columns: 1fr !important; max-width: 440px !important; }
          .login-editorial-col { display: none !important; }
        }
      `}</style>
    </div>
  );
}
