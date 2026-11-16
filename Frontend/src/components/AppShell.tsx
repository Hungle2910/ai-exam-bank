'use client';

import React, { useState } from 'react';
import { ShieldAlert, ArrowLeft, BookOpen } from 'lucide-react';
import { useAuth, AppRole } from '@/contexts/AuthContext';
import { Sidebar, TopBar, ROLE_SHORT_LABEL } from '@/components/layout/Sidebar';
import LoginPage from '@/components/pages/LoginPage';
import DashboardPage from '@/components/pages/DashboardPage';
import SchoolsPage from '@/components/pages/SchoolsPage';
import UsersPage from '@/components/pages/UsersPage';
import RolesPage from '@/components/pages/RolesPage';
import BlueprintsPage from '@/components/pages/BlueprintsPage';
import ReviewPage from '@/components/pages/ReviewPage';
import AuditPage from '@/components/pages/AuditPage';
import QuestionsPage from '@/components/pages/QuestionsPage';
import ImportPage from '@/components/pages/ImportPage';
import GeneratePage from '@/components/pages/GeneratePage';
import ExamsPage from '@/components/pages/ExamsPage';
import OperationsPage from '@/components/pages/OperationsPage';

export const ROUTE_META: Record<string, { section: string; page: string; roles?: AppRole[] }> = {
  '/dashboard':      { section: 'Bàn làm việc', page: 'Việc cần xử lý' },
  '/questions':      { section: 'Mục lục Câu hỏi', page: 'Mục lục câu hỏi', roles: ['Teacher', 'DepartmentHead', 'SchoolAdmin'] },
  '/import':         { section: 'Mục lục Câu hỏi', page: 'Nhập bảng CSV', roles: ['Teacher', 'DepartmentHead'] },
  '/blueprints':     { section: 'Bàn làm việc', page: 'Soạn đề theo ma trận', roles: ['Teacher', 'DepartmentHead'] },
  '/generate':       { section: 'Bàn làm việc', page: 'Rà soát gợi ý AI', roles: ['Teacher', 'DepartmentHead'] },
  '/exams':          { section: 'Bàn làm việc', page: 'Xem trước & Bản chốt' },
  '/review':         { section: 'Bàn Thẩm định', page: 'Duyệt câu hỏi', roles: ['DepartmentHead', 'SchoolAdmin', 'MinistryAdmin'] },
  '/audit':          { section: 'Bàn Thẩm định', page: 'Nhật ký thẩm định', roles: ['DepartmentHead', 'SchoolAdmin', 'MinistryAdmin'] },
  '/ops':            { section: 'Quản trị & Tác vụ', page: 'Tác vụ nền & Hệ thống', roles: ['Teacher', 'DepartmentHead', 'SchoolAdmin', 'MinistryAdmin'] },
  '/admin/schools':  { section: 'Quản trị & Tác vụ', page: 'Trường & Bộ môn', roles: ['SchoolAdmin', 'MinistryAdmin'] },
  '/admin/users':    { section: 'Quản trị & Tác vụ', page: 'Tài khoản cán bộ', roles: ['SchoolAdmin', 'MinistryAdmin'] },
  '/admin/roles':    { section: 'Quản trị & Tác vụ', page: 'Phân quyền', roles: ['SchoolAdmin', 'MinistryAdmin'] },
};

function LoadingScreen() {
  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      background: 'var(--paper-bg)',
      flexDirection: 'column',
      gap: 12,
    }}>
      <div style={{
        width: 36, height: 36,
        background: 'var(--brand-600)',
        borderRadius: 4,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        color: '#FCFBF7',
      }}>
        <BookOpen size={18} />
      </div>
      <div style={{ fontSize: 13, color: 'var(--ink-600)', fontWeight: 500 }}>
        Đang khởi tạo phiên làm việc…
      </div>
    </div>
  );
}

function ForbiddenView({
  route,
  allowedRoles,
  currentRole,
  onBackDashboard,
}: {
  route: string;
  allowedRoles: AppRole[];
  currentRole: AppRole | null;
  onBackDashboard: () => void;
}) {
  return (
    <div className="card" style={{ maxWidth: 520, margin: '48px auto', padding: 28, textAlign: 'center' }}>
      <div
        style={{
          width: 44,
          height: 44,
          borderRadius: 8,
          background: 'var(--danger-50)',
          border: '1px solid #fecaca',
          color: 'var(--danger-600)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          margin: '0 auto 14px',
        }}
      >
        <ShieldAlert size={22} />
      </div>
      <span className="badge badge-red" style={{ marginBottom: 10 }}>
        HTTP 403: Từ chối truy cập theo phạm vi
      </span>
      <h2 style={{ fontSize: 16, fontWeight: 700, color: 'var(--gray-900)', marginBottom: 8 }}>
        Không đủ thẩm quyền mở phân hệ <code>{route}</code>
      </h2>
      <p style={{ fontSize: 13, color: 'var(--gray-600)', lineHeight: 1.6, marginBottom: 18 }}>
        Tài khoản đang giữ vai trò{' '}
        <strong>{currentRole ? ROLE_SHORT_LABEL[currentRole] : 'Chưa gán quyền'}</strong>. Phân hệ này yêu cầu vai trò:{' '}
        <strong>{allowedRoles.map(r => ROLE_SHORT_LABEL[r]).join(', ')}</strong>.
      </p>
      <button className="btn btn-primary" onClick={onBackDashboard} id="btn-back-dashboard-403">
        <ArrowLeft size={14} />
        Quay về Tổng quan học vụ
      </button>
    </div>
  );
}

export default function AppShell() {
  const { user, loading, primaryRole } = useAuth();
  const [route, setRoute] = useState('/dashboard');
  const [sidebarOpen, setSidebarOpen] = useState(false);

  if (loading) return <LoadingScreen />;
  if (!user) return <LoginPage />;

  const meta = ROUTE_META[route] ?? { section: 'Hệ thống', page: route };
  const isAllowed =
    !meta.roles || (primaryRole !== null && meta.roles.includes(primaryRole));

  const renderPage = () => {
    if (!isAllowed && meta.roles) {
      return (
        <ForbiddenView
          route={route}
          allowedRoles={meta.roles}
          currentRole={primaryRole}
          onBackDashboard={() => setRoute('/dashboard')}
        />
      );
    }

    switch (route) {
      case '/dashboard':     return <DashboardPage onNavigate={setRoute} />;
      case '/questions':     return <QuestionsPage onNavigate={setRoute} />;
      case '/import':        return <ImportPage onNavigate={setRoute} />;
      case '/blueprints':    return <BlueprintsPage onNavigate={setRoute} />;
      case '/generate':      return <GeneratePage onNavigate={setRoute} />;
      case '/exams':         return <ExamsPage onNavigate={setRoute} />;
      case '/review':        return <ReviewPage />;
      case '/audit':         return <AuditPage />;
      case '/ops':           return <OperationsPage />;
      case '/admin/schools': return <SchoolsPage onNavigate={setRoute} />;
      case '/admin/users':   return <UsersPage onNavigate={setRoute} />;
      case '/admin/roles':   return <RolesPage />;
      default:               return <DashboardPage onNavigate={setRoute} />;
    }
  };

  return (
    <div className="app-layout">
      <Sidebar
        activeRoute={route}
        onNavigate={setRoute}
        open={sidebarOpen}
        onClose={() => setSidebarOpen(false)}
      />
      <div className="main-content">
        <TopBar
          section={meta.section}
          page={meta.page}
          onMenuClick={() => setSidebarOpen(true)}
          onNavigate={setRoute}
        />
        <main className="page-content" id="main-content" role="main">
          {renderPage()}
        </main>
      </div>

      <style>{`
        @media (max-width: 1024px) {
          #btn-menu-toggle { display: flex !important; }
          #sidebar-close-btn { display: flex !important; }
          .hide-mobile { display: none; }
        }
      `}</style>
    </div>
  );
}
