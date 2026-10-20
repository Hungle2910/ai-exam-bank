'use client';

import React, { useState } from 'react';
import { useAuth } from '@/contexts/AuthContext';
import { Sidebar, TopBar } from '@/components/layout/Sidebar';
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

/* ============================================================
   ROUTE MAP
   ============================================================ */

const ROUTE_META: Record<string, { section: string; page: string }> = {
  '/dashboard':      { section: 'Tổng quan', page: 'Dashboard' },
  '/questions':      { section: 'Ngân hàng câu hỏi', page: 'Câu hỏi' },
  '/import':         { section: 'Ngân hàng câu hỏi', page: 'Import CSV' },
  '/blueprints':     { section: 'Đề thi', page: 'Ma trận đề' },
  '/generate':       { section: 'Đề thi', page: 'Tạo đề AI' },
  '/exams':          { section: 'Đề thi', page: 'Đề thi' },
  '/review':         { section: 'Duyệt', page: 'Chờ duyệt' },
  '/audit':          { section: 'Duyệt', page: 'Audit Log' },
  '/admin/schools':  { section: 'Quản trị', page: 'Trường học' },
  '/admin/users':    { section: 'Quản trị', page: 'Người dùng' },
  '/admin/roles':    { section: 'Quản trị', page: 'Phân quyền' },
};

/* ============================================================
   SHELL — LOADING SCREEN
   ============================================================ */

function LoadingScreen() {
  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      background: 'var(--gray-50)',
      flexDirection: 'column',
      gap: 16,
    }}>
      <div style={{
        width: 48, height: 48,
        background: 'linear-gradient(135deg, var(--brand-600), var(--brand-500))',
        borderRadius: 14,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        animation: 'pulse 1.5s ease-in-out infinite',
        boxShadow: '0 4px 20px rgba(37,99,235,0.3)',
      }}>
        <span style={{ fontSize: 18, fontWeight: 800, color: 'white' }}>AI</span>
      </div>
      <div style={{ fontSize: 13, color: 'var(--gray-400)', fontWeight: 500 }}>
        Đang tải hệ thống…
      </div>
      <style>{`
        @keyframes pulse {
          0%, 100% { transform: scale(1); opacity: 1; }
          50% { transform: scale(0.92); opacity: 0.85; }
        }
      `}</style>
    </div>
  );
}

/* ============================================================
   MAIN APP SHELL
   ============================================================ */

export default function AppShell() {
  const { user, loading } = useAuth();
  const [route, setRoute] = useState('/dashboard');
  const [sidebarOpen, setSidebarOpen] = useState(false);

  if (loading) return <LoadingScreen />;
  if (!user) return <LoginPage />;

  const meta = ROUTE_META[route] ?? { section: 'Hệ thống', page: route };

  const renderPage = () => {
    switch (route) {
      case '/dashboard':     return <DashboardPage />;
      case '/questions':     return <QuestionsPage />;
      case '/import':        return <ImportPage />;
      case '/blueprints':    return <BlueprintsPage />;
      case '/generate':      return <GeneratePage />;
      case '/exams':         return <ExamsPage />;
      case '/review':        return <ReviewPage />;
      case '/audit':         return <AuditPage />;
      case '/admin/schools': return <SchoolsPage />;
      case '/admin/users':   return <UsersPage />;
      case '/admin/roles':   return <RolesPage />;
      default:               return <DashboardPage />;
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
        />
        <main className="page-content" id="main-content" role="main">
          {renderPage()}
        </main>
      </div>

      {/* Responsive: show menu button on mobile */}
      <style>{`
        @media (max-width: 1024px) {
          #btn-menu-toggle { display: flex !important; }
          #sidebar-close-btn { display: flex !important; }
          .search-bar { display: none; }
        }
      `}</style>
    </div>
  );
}
