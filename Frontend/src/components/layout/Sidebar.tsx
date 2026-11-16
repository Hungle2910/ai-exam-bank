'use client';

import React from 'react';
import {
  BookOpen, GraduationCap, Users, Building2, Shield,
  LayoutDashboard, FileText, ClipboardCheck, Upload,
  Cpu, Briefcase, LogOut, Menu, X,
  ChevronRight, Activity, UserCheck
} from 'lucide-react';
import { useAuth, AppRole } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
import { useWorkflowStore } from '@/lib/workflowStore';
import clsx from 'clsx';

export interface NavItem {
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

export const navSections: NavSection[] = [
  {
    title: 'Bàn làm việc',
    items: [
      {
        id: 'dashboard',
        label: 'Việc cần xử lý',
        icon: <LayoutDashboard size={14} />,
        href: '/dashboard',
      },
      {
        id: 'blueprints',
        label: 'Soạn đề theo ma trận',
        icon: <GraduationCap size={14} />,
        href: '/blueprints',
        roles: ['Teacher', 'DepartmentHead'],
      },
      {
        id: 'generate',
        label: 'Rà soát gợi ý AI',
        icon: <Cpu size={14} />,
        href: '/generate',
        roles: ['Teacher', 'DepartmentHead'],
      },
      {
        id: 'exams',
        label: 'Xem trước & Bản chốt',
        icon: <FileText size={14} />,
        href: '/exams',
      },
    ],
  },
  {
    title: 'Mục lục Câu hỏi',
    items: [
      {
        id: 'questions',
        label: 'Mục lục câu hỏi',
        icon: <BookOpen size={14} />,
        href: '/questions',
        roles: ['Teacher', 'DepartmentHead', 'SchoolAdmin'],
      },
      {
        id: 'import',
        label: 'Nhập bảng CSV',
        icon: <Upload size={14} />,
        href: '/import',
        roles: ['Teacher', 'DepartmentHead'],
      },
    ],
  },
  {
    title: 'Bàn Thẩm định',
    items: [
      {
        id: 'review',
        label: 'Duyệt câu hỏi',
        icon: <ClipboardCheck size={14} />,
        href: '/review',
        roles: ['DepartmentHead', 'SchoolAdmin', 'MinistryAdmin'],
      },
      {
        id: 'audit',
        label: 'Nhật ký thẩm định',
        icon: <Shield size={14} />,
        href: '/audit',
        roles: ['DepartmentHead', 'SchoolAdmin', 'MinistryAdmin'],
      },
    ],
  },
  {
    title: 'Quản trị & Tác vụ',
    items: [
      {
        id: 'ops',
        label: 'Tác vụ nền & Hệ thống',
        icon: <Activity size={14} />,
        href: '/ops',
        roles: ['Teacher', 'DepartmentHead', 'SchoolAdmin', 'MinistryAdmin'],
      },
      {
        id: 'schools',
        label: 'Trường & Bộ môn',
        icon: <Building2 size={14} />,
        href: '/admin/schools',
        roles: ['SchoolAdmin', 'MinistryAdmin'],
      },
      {
        id: 'users',
        label: 'Tài khoản cán bộ',
        icon: <Users size={14} />,
        href: '/admin/users',
        roles: ['SchoolAdmin', 'MinistryAdmin'],
      },
      {
        id: 'roles',
        label: 'Phân quyền',
        icon: <Briefcase size={14} />,
        href: '/admin/roles',
        roles: ['SchoolAdmin', 'MinistryAdmin'],
      },
    ],
  },
];

export const ROLE_LABEL: Record<AppRole, string> = {
  MinistryAdmin: 'Quản trị Bộ',
  SchoolAdmin: 'Quản trị Trường',
  DepartmentHead: 'Trưởng Bộ Môn',
  Teacher: 'Giáo viên',
};

export const ROLE_SHORT_LABEL: Record<AppRole, string> = {
  MinistryAdmin: 'Quản trị Bộ',
  SchoolAdmin: 'Quản trị Trường',
  DepartmentHead: 'Trưởng Bộ Môn',
  Teacher: 'Giáo viên',
};

interface SidebarProps {
  activeRoute: string;
  onNavigate: (href: string) => void;
  open: boolean;
  onClose: () => void;
}

export function Sidebar({ activeRoute, onNavigate, open, onClose }: SidebarProps) {
  const { user, primaryRole, logout } = useAuth();
  const { success, error: showError } = useToast();
  const wf = useWorkflowStore();
  const pendingReviewCount = wf.questions.filter(q => q.status === 'REVIEW_REQUIRED').length;

  const handleLogout = async () => {
    try {
      await logout();
      success('Đã đăng xuất thành công');
    } catch {
      showError('Đăng xuất thất bại. Vui lòng thử lại');
    }
  };

  const visibleSections = navSections.filter(section => {
    if (!section.roles) return true;
    if (!primaryRole) return false;
    return section.roles.includes(primaryRole);
  });

  const initials = user?.userName
    ? user.userName.slice(0, 2).toUpperCase()
    : 'GV';

  return (
    <>
      {open && (
        <div
          className="modal-overlay"
          style={{ zIndex: 49 }}
          onClick={onClose}
        />
      )}

      <aside className={clsx('sidebar', open && 'open')}>
        <div className="sidebar-logo">
          <div className="sidebar-logo-icon" aria-hidden="true">
            <svg width="15" height="15" viewBox="0 0 16 16" fill="none">
              <rect x="2.5" y="1.5" width="11" height="13" rx="1" stroke="currentColor" strokeWidth="1.4" />
              <line x1="5" y1="5" x2="11" y2="5" stroke="currentColor" strokeWidth="1.4" />
              <line x1="5" y1="8" x2="11" y2="8" stroke="currentColor" strokeWidth="1.4" />
              <line x1="5" y1="11" x2="8.5" y2="11" stroke="currentColor" strokeWidth="1.4" />
            </svg>
          </div>
          <div className="sidebar-logo-text">
            <span className="sidebar-logo-title">Bàn Soạn Đề</span>
            <span className="sidebar-logo-sub">Sổ ma trận &amp; Thẩm định</span>
          </div>
          <button
            className="topbar-icon-btn"
            onClick={onClose}
            style={{ marginLeft: 'auto', display: 'none' }}
            aria-label="Đóng menu"
            id="sidebar-close-btn"
          >
            <X size={15} />
          </button>
        </div>

        <nav className="sidebar-nav" aria-label="Mục lục bàn soạn đề">
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
                {visibleItems.map(item => {
                  const badgeText =
                    item.id === 'review' && pendingReviewCount > 0
                      ? String(pendingReviewCount)
                      : item.badge;
                  return (
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
                      {badgeText && (
                        <span
                          className={clsx(
                            'badge',
                            item.id === 'review' ? 'badge-red' : 'badge-gray'
                          )}
                          style={{ fontSize: 10.5, padding: '0px 5px', fontFamily: 'var(--font-mono)' }}
                        >
                          {badgeText}
                        </span>
                      )}
                    </button>
                  );
                })}
              </div>
            );
          })}
        </nav>

        <div className="sidebar-footer">
          <div className="sidebar-user">
            <div className="sidebar-avatar">{initials}</div>
            <div className="sidebar-user-info">
              <div className="sidebar-user-name">{user?.userName ?? '—'}</div>
              <div className="sidebar-user-role">
                {primaryRole ? ROLE_SHORT_LABEL[primaryRole] : 'Chưa gán quyền'}
              </div>
            </div>
          </div>
          <button
            className="sidebar-nav-item"
            style={{ width: '100%', textAlign: 'left', color: 'var(--danger-500)', padding: '5px 8px' }}
            onClick={handleLogout}
            id="btn-logout"
          >
            <LogOut size={13} style={{ color: 'var(--danger-500)' }} />
            <span>Đóng phiên làm việc</span>
          </button>
        </div>
      </aside>
    </>
  );
}

interface TopBarProps {
  section: string;
  page: string;
  onMenuClick: () => void;
  onNavigate?: (href: string) => void;
}

export function TopBar({ section, page, onMenuClick, onNavigate }: TopBarProps) {
  const { user, primaryRole, isDemoSession, loginDemoRole } = useAuth();

  return (
    <header className="topbar">
      <button
        className="topbar-icon-btn"
        onClick={onMenuClick}
        aria-label="Mở mục lục"
        id="btn-menu-toggle"
        style={{ display: 'none' }}
      >
        <Menu size={16} />
      </button>

      <div className="topbar-breadcrumb">
        <span className="hide-mobile">{section}</span>
        <ChevronRight size={12} className="separator hide-mobile" />
        <span className="current">{page}</span>
      </div>

      <div className="topbar-actions">
        {isDemoSession && loginDemoRole ? (
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              background: 'var(--paper-sheet)',
              border: '1px solid var(--rule-strong)',
              padding: '2px 4px 2px 8px',
              borderRadius: 3,
            }}
          >
            <UserCheck size={12} style={{ color: 'var(--brand-600)' }} />
            <code className="hide-mobile" style={{ color: 'var(--ink-900)', fontWeight: 600, fontSize: 11.5 }}>
              {user?.userName}
            </code>
            <select
              className="form-select"
              style={{
                height: 24,
                padding: '0 20px 0 6px',
                fontSize: 11.5,
                width: 'auto',
                fontWeight: 600,
                background: 'var(--brand-50)',
                color: 'var(--brand-700)',
                borderColor: 'var(--brand-200)',
              }}
              value={primaryRole ?? 'Teacher'}
              onChange={e => loginDemoRole(e.target.value as AppRole)}
              aria-label="Chuyển đổi vai trò kiểm thử"
              id="demo-role-switcher"
            >
              <option value="Teacher">Giáo viên</option>
              <option value="DepartmentHead">Trưởng Bộ Môn</option>
              <option value="SchoolAdmin">Quản trị Trường</option>
              <option value="MinistryAdmin">Quản trị Bộ</option>
            </select>
          </div>
        ) : (
          user && (
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                fontSize: 12,
                color: 'var(--ink-700)',
                background: 'var(--paper-sheet)',
                border: '1px solid var(--rule-base)',
                padding: '2px 8px',
                borderRadius: 3,
              }}
            >
              <UserCheck size={12} style={{ color: 'var(--brand-600)' }} />
              <code style={{ color: 'var(--ink-900)', fontWeight: 600, fontSize: 11.5 }}>{user.userName}</code>
              {primaryRole && (
                <span className="badge badge-green" style={{ fontSize: 10.5, padding: '0 5px' }}>
                  {ROLE_SHORT_LABEL[primaryRole]}
                </span>
              )}
            </div>
          )
        )}

        {onNavigate && (
          <button
            className="btn btn-secondary btn-sm"
            onClick={() => onNavigate('/ops')}
            id="btn-topbar-ops"
            title="Theo dõi tác vụ nền & hạ tầng"
          >
            <Activity size={12} />
            <span className="hide-mobile">Tác vụ</span>
          </button>
        )}
      </div>
    </header>
  );
}
