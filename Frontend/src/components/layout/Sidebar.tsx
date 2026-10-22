'use client';

import React, { useState } from 'react';
import {
  BookOpen, GraduationCap, Users, Building2, Shield,
  LayoutDashboard, FileText, ClipboardCheck, Upload,
  Cpu, Briefcase, Bell, Settings, LogOut, Menu, X,
  ChevronRight, HelpCircle
} from 'lucide-react';
import { useAuth, AppRole } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
import clsx from 'clsx';

/* ============================================================
   NAVIGATION CONFIG
   ============================================================ */

interface NavItem {
  id: string;
  label: string;
  icon: React.ReactNode;
  href: string;
  roles?: AppRole[];
  badge?: string;
}

interface NavSection {
  title: string;
  items: NavItem[];
  roles?: AppRole[];
}

const navSections: NavSection[] = [
  {
    title: 'Tổng quan',
    items: [
      {
        id: 'dashboard',
        label: 'Dashboard',
        icon: <LayoutDashboard size={16} />,
        href: '/dashboard',
      },
    ],
  },
  {
    title: 'Ngân hàng câu hỏi',
    items: [
      {
        id: 'questions',
        label: 'Câu hỏi',
        icon: <BookOpen size={16} />,
        href: '/questions',
        roles: ['Teacher', 'DepartmentHead', 'SchoolAdmin'],
      },
      {
        id: 'import',
        label: 'Import CSV',
        icon: <Upload size={16} />,
        href: '/import',
        roles: ['Teacher', 'DepartmentHead'],
      },
    ],
  },
  {
    title: 'Đề thi',
    items: [
      {
        id: 'blueprints',
        label: 'Ma trận đề',
        icon: <GraduationCap size={16} />,
        href: '/blueprints',
        roles: ['Teacher', 'DepartmentHead'],
      },
      {
        id: 'generate',
        label: 'Tạo đề AI',
        icon: <Cpu size={16} />,
        href: '/generate',
        roles: ['Teacher', 'DepartmentHead'],
        badge: 'AI',
      },
      {
        id: 'exams',
        label: 'Đề thi',
        icon: <FileText size={16} />,
        href: '/exams',
      },
    ],
  },
  {
    title: 'Duyệt & Kiểm duyệt',
    items: [
      {
        id: 'review',
        label: 'Chờ duyệt',
        icon: <ClipboardCheck size={16} />,
        href: '/review',
        roles: ['DepartmentHead', 'SchoolAdmin', 'MinistryAdmin'],
      },
      {
        id: 'audit',
        label: 'Audit Log',
        icon: <Shield size={16} />,
        href: '/audit',
        roles: ['SchoolAdmin', 'MinistryAdmin'],
      },
    ],
  },
  {
    title: 'Quản trị',
    roles: ['SchoolAdmin', 'MinistryAdmin'],
    items: [
      {
        id: 'schools',
        label: 'Trường học',
        icon: <Building2 size={16} />,
        href: '/admin/schools',
        roles: ['SchoolAdmin', 'MinistryAdmin'],
      },
      {
        id: 'users',
        label: 'Người dùng',
        icon: <Users size={16} />,
        href: '/admin/users',
        roles: ['SchoolAdmin', 'MinistryAdmin'],
      },
      {
        id: 'roles',
        label: 'Phân quyền',
        icon: <Briefcase size={16} />,
        href: '/admin/roles',
        roles: ['SchoolAdmin', 'MinistryAdmin'],
      },
    ],
  },
];

/* ============================================================
   ROLE DISPLAY
   ============================================================ */

const ROLE_LABEL: Record<AppRole, string> = {
  MinistryAdmin: 'Quản trị Bộ',
  SchoolAdmin: 'Quản trị Trường',
  DepartmentHead: 'Trưởng Bộ Môn',
  Teacher: 'Giáo viên',
};

const ROLE_COLOR: Record<AppRole, string> = {
  MinistryAdmin: 'badge-purple',
  SchoolAdmin: 'badge-blue',
  DepartmentHead: 'badge-teal',
  Teacher: 'badge-green',
};

/* ============================================================
   SIDEBAR COMPONENT
   ============================================================ */

interface SidebarProps {
  activeRoute: string;
  onNavigate: (href: string) => void;
  open: boolean;
  onClose: () => void;
}

export function Sidebar({ activeRoute, onNavigate, open, onClose }: SidebarProps) {
  const { user, primaryRole, logout } = useAuth();
  const { success } = useToast();

  const handleLogout = async () => {
    await logout();
    success('Đã đăng xuất thành công');
  };

  const visibleSections = navSections.filter(section => {
    if (!section.roles) return true;
    if (!primaryRole) return false;
    return section.roles.includes(primaryRole);
  });

  const initials = user?.userName
    ? user.userName.slice(0, 2).toUpperCase()
    : '??';

  return (
    <>
      {/* Mobile overlay */}
      {open && (
        <div
          className="modal-overlay"
          style={{ zIndex: 49, backdropFilter: 'blur(2px)' }}
          onClick={onClose}
        />
      )}

      <aside className={clsx('sidebar', open && 'open')}>
        {/* Logo */}
        <div className="sidebar-logo">
          <div className="sidebar-logo-icon">
            <span style={{ fontSize: 14, fontWeight: 800 }}>AI</span>
          </div>
          <div className="sidebar-logo-text">
            <span className="sidebar-logo-title">Exam Bank</span>
            <span className="sidebar-logo-sub">AI-Powered</span>
          </div>
          <button
            className="topbar-icon-btn"
            onClick={onClose}
            style={{ marginLeft: 'auto', display: 'none' }}
            aria-label="Close sidebar"
            id="sidebar-close-btn"
          >
            <X size={16} />
          </button>
        </div>

        {/* Navigation */}
        <nav className="sidebar-nav" aria-label="Main navigation">
          {visibleSections.map(section => {
            const visibleItems = section.items.filter(item => {
              if (!item.roles) return true;
              if (!primaryRole) return false;
              return item.roles.includes(primaryRole);
            });
            if (visibleItems.length === 0) return null;

            return (
              <div key={section.title}>
                <div className="sidebar-section-label">{section.title}</div>
                {visibleItems.map(item => (
                  <button
                    key={item.id}
                    id={`nav-${item.id}`}
                    className={clsx('sidebar-nav-item', activeRoute === item.href && 'active')}
                    onClick={() => { onNavigate(item.href); onClose(); }}
                    aria-current={activeRoute === item.href ? 'page' : undefined}
                    style={{ width: '100%', textAlign: 'left' }}
                  >
                    {item.icon}
                    <span style={{ flex: 1 }}>{item.label}</span>
                    {item.badge && (
                      <span className={clsx(
                        'badge badge-sm',
                        item.badge === 'AI' ? 'badge-purple' : 'badge-gray'
                      )} style={{ fontSize: 10, padding: '1px 6px' }}>
                        {item.badge}
                      </span>
                    )}
                  </button>
                ))}
              </div>
            );
          })}
        </nav>

        {/* Footer */}
        <div className="sidebar-footer">
          <button
            className="sidebar-nav-item"
            style={{ width: '100%', textAlign: 'left' }}
            id="nav-help"
          >
            <HelpCircle size={16} />
            <span>Trợ giúp & Tài liệu</span>
          </button>
          <div className="divider" style={{ margin: '8px 0' }} />
          <div className="sidebar-user">
            <div className="sidebar-avatar">{initials}</div>
            <div className="sidebar-user-info">
              <div className="sidebar-user-name">{user?.userName ?? '—'}</div>
              <div className="sidebar-user-role">
                {primaryRole ? ROLE_LABEL[primaryRole] : '—'}
              </div>
            </div>
          </div>
          <button
            className="sidebar-nav-item"
            style={{ width: '100%', textAlign: 'left', color: 'var(--danger-600)' }}
            onClick={handleLogout}
            id="btn-logout"
          >
            <LogOut size={16} style={{ color: 'var(--danger-400)' }} />
            <span>Đăng xuất</span>
          </button>
        </div>
      </aside>
    </>
  );
}

/* ============================================================
   TOP BAR
   ============================================================ */

interface TopBarProps {
  section: string;
  page: string;
  onMenuClick: () => void;
}

export function TopBar({ section, page, onMenuClick }: TopBarProps) {
  return (
    <header className="topbar">
      <button
        className="topbar-icon-btn"
        onClick={onMenuClick}
        aria-label="Toggle menu"
        id="btn-menu-toggle"
        style={{ display: 'none' }}
      >
        <Menu size={18} />
      </button>

      <div className="topbar-breadcrumb">
        <span>{section}</span>
        <ChevronRight size={13} className="separator" />
        <span className="current">{page}</span>
      </div>

      <div className="topbar-actions">
        <div className="search-bar">
          <svg className="search-bar-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/>
          </svg>
          <input type="text" placeholder="Tìm kiếm..." id="topbar-search" aria-label="Search" />
        </div>

        <button className="topbar-icon-btn" id="btn-notifications" aria-label="Notifications">
          <Bell size={16} />
          <span className="notification-dot" />
        </button>

        <button className="topbar-icon-btn" id="btn-settings" aria-label="Settings">
          <Settings size={16} />
        </button>
      </div>
    </header>
  );
}
