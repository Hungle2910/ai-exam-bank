'use client';

import React, { useState } from 'react';
import { BookOpen, Cpu, GraduationCap, ClipboardCheck, Users, Building2, TrendingUp, AlertCircle, CheckCircle, Clock, FileText } from 'lucide-react';
import { useAuth } from '@/contexts/AuthContext';

/* ============================================================
   DASHBOARD OVERVIEW PAGE
   ============================================================ */

export default function DashboardPage() {
  const { user, primaryRole } = useAuth();

  const isAdmin = primaryRole === 'MinistryAdmin' || primaryRole === 'SchoolAdmin';
  const isTeacher = primaryRole === 'Teacher' || primaryRole === 'DepartmentHead';

  return (
    <div>
      {/* Welcome Header */}
      <div className="page-header">
        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 16 }}>
          <div>
            <h1 className="page-title">
              Xin chào, {user?.userName} 👋
            </h1>
            <p className="page-subtitle">
              Đây là tổng quan hoạt động hệ thống hôm nay.
            </p>
          </div>
          <span className="badge badge-blue" style={{ padding: '6px 12px', fontSize: 12 }}>
            {primaryRole === 'MinistryAdmin' ? 'Quản trị Bộ'
              : primaryRole === 'SchoolAdmin' ? 'Quản trị Trường'
              : primaryRole === 'DepartmentHead' ? 'Trưởng Bộ Môn'
              : 'Giáo viên'}
          </span>
        </div>
      </div>

      {/* Stats Grid */}
      <div className="stats-grid">
        {isTeacher && (
          <>
            <StatCard
              icon={<BookOpen />}
              iconClass="blue"
              value="142"
              label="Câu hỏi của tôi"
              change="+8 tuần này"
              changeDir="up"
            />
            <StatCard
              icon={<CheckCircle />}
              iconClass="green"
              value="98"
              label="Đã được duyệt"
              change="69%"
              changeDir="up"
            />
            <StatCard
              icon={<Clock />}
              iconClass="amber"
              value="12"
              label="Chờ duyệt"
              change="3 mới hôm nay"
              changeDir="up"
            />
            <StatCard
              icon={<FileText />}
              iconClass="purple"
              value="5"
              label="Đề thi đã tạo"
              change="+1 tuần này"
              changeDir="up"
            />
          </>
        )}

        {isAdmin && (
          <>
            <StatCard
              icon={<Building2 />}
              iconClass="blue"
              value="12"
              label="Trường học"
              change="Toàn quốc"
              changeDir="up"
            />
            <StatCard
              icon={<Users />}
              iconClass="green"
              value="348"
              label="Người dùng"
              change="+24 tháng này"
              changeDir="up"
            />
            <StatCard
              icon={<BookOpen />}
              iconClass="amber"
              value="8,452"
              label="Tổng câu hỏi"
              change="+312 tuần này"
              changeDir="up"
            />
            <StatCard
              icon={<ClipboardCheck />}
              iconClass="red"
              value="67"
              label="Chờ duyệt"
              change="Cần xử lý"
              changeDir="down"
            />
          </>
        )}
      </div>

      {/* Main grid */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 360px', gap: 20 }}>
        {/* Recent Activity */}
        <div className="card">
          <div className="card-header">
            <div>
              <div className="card-title">Hoạt động gần đây</div>
              <div className="card-subtitle">Các thay đổi trong 7 ngày qua</div>
            </div>
          </div>
          <div className="card-body" style={{ padding: 0 }}>
            <ActivityList />
          </div>
        </div>

        {/* Right column */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          {/* AI Generation Status */}
          <div className="card">
            <div className="card-header">
              <div className="card-title">
                <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <Cpu size={15} style={{ color: 'var(--brand-500)' }} />
                  Trạng thái AI
                </span>
              </div>
              <span className="badge badge-green">
                <span className="badge-dot" />
                Online
              </span>
            </div>
            <div className="card-body">
              <AIStatusPanel />
            </div>
          </div>

          {/* Quick Stats */}
          <div className="card">
            <div className="card-header">
              <div className="card-title">Phân phối câu hỏi</div>
            </div>
            <div className="card-body">
              <QuestionDistribution />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

/* ============================================================
   STAT CARD
   ============================================================ */

interface StatCardProps {
  icon: React.ReactNode;
  iconClass: 'blue' | 'green' | 'amber' | 'red' | 'purple';
  value: string;
  label: string;
  change?: string;
  changeDir?: 'up' | 'down';
}

function StatCard({ icon, iconClass, value, label, change, changeDir }: StatCardProps) {
  return (
    <div className="stat-card">
      <div className={`stat-icon ${iconClass}`}>{icon}</div>
      <div className="stat-content">
        <div className="stat-value">{value}</div>
        <div className="stat-label">{label}</div>
        {change && (
          <div className={`stat-change ${changeDir}`}>
            {changeDir === 'up' ? '↑' : '↓'} {change}
          </div>
        )}
      </div>
    </div>
  );
}

/* ============================================================
   ACTIVITY LIST
   ============================================================ */

const activities = [
  { id: 1, type: 'approved', user: 'GV Minh Tuấn', action: 'Câu hỏi Toán 12 được duyệt', time: '5 phút trước', color: 'var(--accent-500)' },
  { id: 2, type: 'submitted', user: 'GV Lan Anh', action: 'Nộp 8 câu hỏi Vật lý 11', time: '23 phút trước', color: 'var(--brand-500)' },
  { id: 3, type: 'generated', user: 'Hệ thống AI', action: 'Tạo đề nháp ma trận Hóa 12', time: '1 giờ trước', color: '#7c3aed' },
  { id: 4, type: 'rejected', user: 'TBỘM Phúc', action: 'Trả lại 3 câu hỏi cần sửa', time: '2 giờ trước', color: 'var(--danger-500)' },
  { id: 5, type: 'imported', user: 'GV Hùng', action: 'Import 45 câu Ngữ văn từ CSV', time: '3 giờ trước', color: 'var(--warning-500)' },
  { id: 6, type: 'created', user: 'GV Thu Hà', action: 'Tạo ma trận đề Sinh 10', time: 'Hôm qua', color: 'var(--accent-500)' },
];

function ActivityList() {
  return (
    <div style={{ padding: '4px 0' }}>
      {activities.map((a, i) => (
        <div key={a.id} style={{
          display: 'flex',
          alignItems: 'flex-start',
          gap: 12,
          padding: '12px 20px',
          borderBottom: i < activities.length - 1 ? '1px solid var(--gray-100)' : 'none',
        }}>
          <div style={{
            width: 8,
            height: 8,
            borderRadius: '50%',
            background: a.color,
            marginTop: 5,
            flexShrink: 0,
          }} />
          <div style={{ flex: 1 }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--gray-800)' }}>
              {a.action}
            </div>
            <div style={{ fontSize: 12, color: 'var(--gray-400)', marginTop: 2 }}>
              {a.user} · {a.time}
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

/* ============================================================
   AI STATUS PANEL
   ============================================================ */

function AIStatusPanel() {
  const metrics = [
    { label: 'Câu hỏi đã tạo', value: '1,248', color: 'var(--brand-500)' },
    { label: 'Đang xử lý', value: '3', color: 'var(--warning-500)' },
    { label: 'Tỷ lệ duyệt', value: '87%', color: 'var(--accent-500)' },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      {metrics.map(m => (
        <div key={m.label} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ fontSize: 12.5, color: 'var(--gray-600)' }}>{m.label}</span>
          <span style={{ fontSize: 14, fontWeight: 700, color: m.color }}>{m.value}</span>
        </div>
      ))}
      <div className="divider" style={{ margin: '4px 0' }} />
      <div style={{ background: 'var(--brand-50)', borderRadius: 'var(--radius-sm)', padding: '10px 12px' }}>
        <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--brand-700)', marginBottom: 4 }}>
          Bedrock RAG — Active
        </div>
        <div style={{ fontSize: 11.5, color: 'var(--brand-600)' }}>
          Knowledge base đã đồng bộ 2h trước
        </div>
      </div>
    </div>
  );
}

/* ============================================================
   QUESTION DISTRIBUTION
   ============================================================ */

const distribution = [
  { subject: 'Toán học', count: 2840, percent: 34, color: 'var(--brand-500)' },
  { subject: 'Vật lý', count: 1920, percent: 23, color: '#7c3aed' },
  { subject: 'Hóa học', count: 1680, percent: 20, color: 'var(--accent-500)' },
  { subject: 'Sinh học', count: 980, percent: 12, color: 'var(--warning-500)' },
  { subject: 'Khác', count: 1032, percent: 12, color: 'var(--gray-300)' },
];

function QuestionDistribution() {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      {distribution.map(d => (
        <div key={d.subject}>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 5 }}>
            <span style={{ fontSize: 12.5, color: 'var(--gray-700)', fontWeight: 500 }}>{d.subject}</span>
            <span style={{ fontSize: 12, color: 'var(--gray-500)' }}>{d.count.toLocaleString()}</span>
          </div>
          <div className="progress-bar">
            <div
              className="progress-fill"
              style={{ width: `${d.percent}%`, background: d.color }}
            />
          </div>
        </div>
      ))}
    </div>
  );
}
