'use client';

import React, { useState, useEffect } from 'react';
import { BookOpen, FileText, Upload, Cpu, ClipboardCheck, Shield } from 'lucide-react';
import { useAuth } from '@/contexts/AuthContext';

/* ============================================================
   PLACEHOLDER PAGES (for features not yet implemented)
   ============================================================ */

interface PlaceholderPageProps {
  icon: React.ReactNode;
  title: string;
  description: string;
  statusLabel: string;
  statusColor: string;
  features: string[];
}

export function PlaceholderPage({
  icon,
  title,
  description,
  statusLabel,
  statusColor,
  features,
}: PlaceholderPageProps) {
  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">{title}</h1>
        <p className="page-subtitle">{description}</p>
      </div>

      <div style={{
        background: 'white',
        border: '1px solid var(--gray-200)',
        borderRadius: 'var(--radius-xl)',
        padding: '60px 48px',
        textAlign: 'center',
        maxWidth: 560,
      }}>
        <div style={{
          width: 72, height: 72,
          borderRadius: 'var(--radius-xl)',
          background: 'var(--brand-50)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          margin: '0 auto 20px',
          color: 'var(--brand-500)',
        }}>
          {icon}
        </div>

        <div style={{ marginBottom: 8 }}>
          <span
            className="badge"
            style={{
              background: statusColor === 'blue' ? 'var(--brand-50)' : statusColor === 'amber' ? 'var(--warning-50)' : 'var(--gray-100)',
              color: statusColor === 'blue' ? 'var(--brand-700)' : statusColor === 'amber' ? 'var(--warning-600)' : 'var(--gray-600)',
              fontSize: 12,
            }}
          >
            {statusLabel}
          </span>
        </div>

        <h2 style={{ fontSize: 18, fontWeight: 700, color: 'var(--gray-900)', marginBottom: 10, letterSpacing: '-0.02em' }}>
          {title} đang được phát triển
        </h2>

        <p style={{ fontSize: 13.5, color: 'var(--gray-500)', lineHeight: 1.7, marginBottom: 24 }}>
          Tính năng này sẽ ra mắt trong các sprint tiếp theo.
          Theo dõi tiến độ trên <strong>GitHub Project Board</strong>.
        </p>

        <div style={{
          background: 'var(--gray-50)',
          borderRadius: 'var(--radius-md)',
          padding: '16px 20px',
          textAlign: 'left',
        }}>
          <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--gray-500)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 10 }}>
            Scope dự kiến
          </div>
          {features.map((f, i) => (
            <div key={i} style={{ display: 'flex', alignItems: 'flex-start', gap: 8, marginBottom: 6 }}>
              <span style={{ color: 'var(--brand-500)', fontWeight: 700, fontSize: 14, marginTop: -1 }}>·</span>
              <span style={{ fontSize: 13, color: 'var(--gray-600)' }}>{f}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

/* ============================================================
   SPECIFIC PLACEHOLDER PAGES
   ============================================================ */

export function QuestionsPage() {
  return (
    <PlaceholderPage
      icon={<BookOpen size={30} />}
      title="Ngân hàng câu hỏi"
      description="CRUD câu hỏi, metadata, tìm kiếm, lọc và quản lý phiên bản"
      statusLabel="Sprint W03"
      statusColor="blue"
      features={[
        'Tạo, sửa, xem và xóa câu hỏi trắc nghiệm',
        'Metadata: môn học, chủ đề, độ khó, điểm số',
        'Phiên bản immutable: mỗi chỉnh sửa tạo revision mới',
        'Tìm kiếm full-text và lọc đa điều kiện',
        'Xem lịch sử revision và trạng thái duyệt',
      ]}
    />
  );
}

export function ImportPage() {
  return (
    <PlaceholderPage
      icon={<Upload size={30} />}
      title="Import CSV"
      description="Upload và import hàng loạt câu hỏi từ file CSV với validation từng dòng"
      statusLabel="Sprint W03"
      statusColor="blue"
      features={[
        'Upload file CSV theo schema chuẩn',
        'Preview và validation từng dòng trước khi import',
        'Báo cáo lỗi chi tiết theo row/column',
        'Idempotent commit: import lại không tạo duplicate',
        'Tracking trạng thái import job realtime',
      ]}
    />
  );
}

export function BlueprintsPage() {
  return (
    <PlaceholderPage
      icon={<FileText size={30} />}
      title="Ma trận đề"
      description="Thiết kế ma trận đề với ràng buộc môn/chủ đề/độ khó/số câu/điểm"
      statusLabel="Sprint W02"
      statusColor="amber"
      features={[
        'Tạo và quản lý blueprint (ma trận đề)',
        'Định nghĩa BlueprintSlot: môn, chủ đề, độ khó, số câu, điểm',
        'Kiểm tra tổng điểm và ràng buộc logic',
        'Chọn câu hỏi đã được duyệt tự động',
        'Xem báo cáo thiếu slot trước khi tạo đề',
      ]}
    />
  );
}

export function GeneratePage() {
  return (
    <PlaceholderPage
      icon={<Cpu size={30} />}
      title="Tạo đề AI"
      description="Amazon Bedrock RAG tự động gợi ý câu hỏi cho các slot còn thiếu"
      statusLabel="Sprint W03-W04"
      statusColor="blue"
      features={[
        'Phát hiện các slot thiếu câu hỏi đủ điều kiện',
        'Bedrock RAG tạo draft câu hỏi kèm trích dẫn nguồn',
        'Giáo viên review và accept/sửa/bỏ từng gợi ý',
        'AI chỉ tạo DRAFT — con người vẫn là người duyệt cuối',
        'Tracking job durable với retry và idempotency',
      ]}
    />
  );
}

export function ExamsPage() {
  return (
    <PlaceholderPage
      icon={<FileText size={30} />}
      title="Đề thi"
      description="Lắp ráp đề thi hoàn chỉnh, validate ma trận và tạo snapshot bất biến"
      statusLabel="Sprint W03-W04"
      statusColor="blue"
      features={[
        'Tổng hợp câu hỏi đã duyệt theo ma trận',
        'Validate số câu, điểm và tính duy nhất',
        'Tạo final exam snapshot version-pinned',
        'Preview đề thi định dạng xuất bản',
        'Đề thi cấp Bộ yêu cầu tác giả và người xác nhận khác nhau',
      ]}
    />
  );
}

export function ReviewPage() {
  return (
    <PlaceholderPage
      icon={<ClipboardCheck size={30} />}
      title="Chờ duyệt"
      description="Xem xét và phê duyệt hoặc trả lại câu hỏi chờ duyệt"
      statusLabel="Sprint W02-W03"
      statusColor="amber"
      features={[
        'Danh sách câu hỏi đang chờ duyệt (REVIEW_REQUIRED)',
        'Xem toàn bộ nội dung và metadata của revision',
        'Chấp nhận hoặc từ chối kèm lý do bắt buộc',
        'Concurrency control: hai người không duyệt cùng lúc',
        'Audit trail mọi quyết định với thời gian và tác nhân',
      ]}
    />
  );
}

export function AuditPage() {
  return (
    <PlaceholderPage
      icon={<Shield size={30} />}
      title="Audit Log"
      description="Lịch sử mọi thao tác phân quyền và duyệt trong hệ thống"
      statusLabel="Sprint W02"
      statusColor="amber"
      features={[
        'Log mọi thao tác Grant/Revoke quyền',
        'Log mọi quyết định Approve/Reject câu hỏi',
        'Lọc theo thời gian, tác nhân, đối tượng',
        'Xuất báo cáo audit theo trường hoặc toàn hệ thống',
        'MinistryAdmin xem audit toàn hệ thống',
      ]}
    />
  );
}
