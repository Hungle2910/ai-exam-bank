'use client';

import React, { useEffect, useState, useCallback } from 'react';
import { FileText, Plus, RefreshCw, Database, Eye, Lock, CheckCircle2, AlertTriangle, ArrowLeft, Printer } from 'lucide-react';
import clsx from 'clsx';
import { useToast } from '@/contexts/ToastContext';
import { fetchOrMock, formatDateTime } from '@/lib/fetchOrMock';

/* ============================================================
   TYPES & MOCK
   ============================================================ */

type ExamStatus = 'ASSEMBLING' | 'VALIDATED' | 'FINALIZED';

interface ExamQuestion { id: string; revision: number; content: string; options: string[]; points: number }
interface Exam {
  id: string; title: string; blueprint: string; status: ExamStatus; durationMinutes: number;
  totalPoints: number; author: string; confirmedBy?: string; updatedAt: string; questions: ExamQuestion[]; issues: string[];
}

const QS: ExamQuestion[] = [
  { id: 'q-1004', revision: 1, content: 'Số nghiệm thực của phương trình log₂(x² − 1) = 3 là?', options: ['0', '1', '2', '3'], points: 0.5 },
  { id: 'q-1001', revision: 2, content: 'Đạo hàm của hàm số y = x³ − 3x + 2 là?', options: ['3x² − 3', '3x² + 3', 'x² − 3', '3x − 3'], points: 0.25 },
  { id: 'q-1006', revision: 1, content: 'Cho cấp số cộng u₁ = 2, d = 3. Số hạng u₁₀ bằng?', options: ['29', '30', '32', '27'], points: 0.25 },
];

const MOCK: Exam[] = [
  { id: 'ex-01', title: 'Kiểm tra giữa kỳ I – Toán 12 – Mã 101', blueprint: 'Giữa kỳ I – Toán 12', status: 'FINALIZED', durationMinutes: 90, totalPoints: 10, author: 'tbm.toan', confirmedBy: 'admin.thpt.a', updatedAt: '2026-10-16T10:00:00Z', questions: QS, issues: [] },
  { id: 'ex-02', title: 'Kiểm tra giữa kỳ I – Toán 12 – Mã 102', blueprint: 'Giữa kỳ I – Toán 12', status: 'VALIDATED', durationMinutes: 90, totalPoints: 10, author: 'tbm.toan', updatedAt: '2026-10-16T11:30:00Z', questions: QS, issues: [] },
  { id: 'ex-03', title: 'Ôn tập Hóa 11 – Chương 1', blueprint: 'Đề ôn tập Hóa 11', status: 'ASSEMBLING', durationMinutes: 45, totalPoints: 10, author: 'gv.tran.thi.b', updatedAt: '2026-10-17T02:00:00Z', questions: QS.slice(0, 2), issues: ['Slot "Liên kết hóa học – Trung bình" thiếu 4 câu', 'Tổng điểm hiện tại 7.5/10'] },
];

const STATUS: Record<ExamStatus, [string, string]> = {
  ASSEMBLING: ['Đang lắp ráp', 'badge-amber'], VALIDATED: ['Đã kiểm tra', 'badge-blue'], FINALIZED: ['Đã chốt', 'badge-green'],
};

/* ============================================================
   PAGE
   ============================================================ */

export default function ExamsPage() {
  const { success, error: showError } = useToast();
  const [exams, setExams] = useState<Exam[]>([]);
  const [isMock, setIsMock] = useState(false);
  const [loading, setLoading] = useState(true);
  const [preview, setPreview] = useState<Exam | null>(null);
  const [filter, setFilter] = useState<'' | ExamStatus>('');

  const load = useCallback(async () => {
    setLoading(true);
    const r = await fetchOrMock<Exam[]>('/api/v1/exams', MOCK);
    setExams(r.data); setIsMock(r.isMock); setLoading(false);
  }, []);
  useEffect(() => { load(); }, [load]);

  const finalize = (e: Exam) => {
    // Ministry-level rule: author and confirmer must differ (mock current user = admin.thpt.a)
    const me = 'admin.thpt.a';
    if (e.author === me) { showError('Người xác nhận phải khác tác giả đề'); return; }
    setExams(p => p.map(x => x.id === e.id ? { ...x, status: 'FINALIZED', confirmedBy: me, updatedAt: new Date().toISOString() } : x));
    setPreview(null);
    success('Đã chốt đề — snapshot bất biến đã được tạo');
  };

  if (preview) return <ExamPreview exam={preview} onBack={() => setPreview(null)} onFinalize={() => finalize(preview)} />;

  const shown = exams.filter(e => !filter || e.status === filter);

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Đề thi</h1>
        <p className="page-subtitle">Lắp ráp đề theo ma trận, kiểm tra và chốt snapshot bất biến</p>
        <div className="page-actions">
          {isMock && <span className="badge badge-amber"><Database size={12} />Dữ liệu mẫu</span>}
          <button className="btn btn-secondary" onClick={load} id="btn-refresh-exams"><RefreshCw size={14} /></button>
          <button className="btn btn-primary" id="btn-create-exam" onClick={() => success('Chọn một ma trận ở trang “Ma trận đề” để lắp ráp đề mới')}><Plus size={14} />Tạo đề</button>
        </div>
      </div>

      <div className="tabs">
        {([['', 'Tất cả'], ['ASSEMBLING', 'Đang lắp ráp'], ['VALIDATED', 'Đã kiểm tra'], ['FINALIZED', 'Đã chốt']] as [typeof filter, string][]).map(([k, l]) => (
          <div key={k || 'all'} className={clsx('tab-item', filter === k && 'active')} onClick={() => setFilter(k)} id={`tab-exam-${k || 'all'}`}>
            {l}<span className="tab-count">{k ? exams.filter(e => e.status === k).length : exams.length}</span>
          </div>
        ))}
      </div>

      {loading ? (
        <div style={{ display: 'grid', gap: 12 }}>{[0, 1, 2].map(i => <div key={i} className="skeleton" style={{ height: 96, borderRadius: 12 }} />)}</div>
      ) : shown.length === 0 ? (
        <div className="table-container"><div className="table-empty">
          <div className="table-empty-icon"><FileText /></div><div className="table-empty-text">Chưa có đề thi</div>
        </div></div>
      ) : (
        <div style={{ display: 'grid', gap: 12 }}>
          {shown.map(e => {
            const pts = e.questions.reduce((a, q) => a + q.points, 0);
            return (
              <div key={e.id} className="card exam-row" style={{ padding: 18, display: 'flex', gap: 16, alignItems: 'center', transition: 'all .2s' }}>
                <div style={{ width: 44, height: 44, borderRadius: 12, flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center',
                  background: e.status === 'FINALIZED' ? 'var(--accent-50)' : 'var(--brand-50)', color: e.status === 'FINALIZED' ? 'var(--accent-600)' : 'var(--brand-600)' }}>
                  {e.status === 'FINALIZED' ? <Lock size={20} /> : <FileText size={20} />}
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 4, flexWrap: 'wrap' }}>
                    <span style={{ fontWeight: 700, color: 'var(--gray-900)' }}>{e.title}</span>
                    <span className={clsx('badge', STATUS[e.status][1])}><span className="badge-dot" />{STATUS[e.status][0]}</span>
                  </div>
                  <div style={{ fontSize: 12.5, color: 'var(--gray-500)' }}>
                    {e.blueprint} · {e.questions.length} câu · {pts}/{e.totalPoints} điểm · {e.durationMinutes} phút · {e.author}{e.confirmedBy && ` → xác nhận bởi ${e.confirmedBy}`} · {formatDateTime(e.updatedAt)}
                  </div>
                  {e.issues.length > 0 && (
                    <div style={{ display: 'flex', gap: 6, marginTop: 6, fontSize: 12, color: 'var(--warning-600)' }}><AlertTriangle size={13} />{e.issues.join(' · ')}</div>
                  )}
                </div>
                <button className="btn btn-secondary btn-sm" onClick={() => setPreview(e)} id={`btn-preview-${e.id}`}><Eye size={13} />Xem</button>
              </div>
            );
          })}
        </div>
      )}
      <style>{`.exam-row:hover { box-shadow: 0 6px 20px rgba(15,23,42,.06); transform: translateY(-1px); }`}</style>
    </div>
  );
}

/* ============================================================
   PREVIEW (print layout)
   ============================================================ */

function ExamPreview({ exam, onBack, onFinalize }: { exam: Exam; onBack: () => void; onFinalize: () => void }) {
  return (
    <div>
      <div className="page-header no-print">
        <button className="btn btn-ghost btn-sm" onClick={onBack} id="btn-back-exams" style={{ marginBottom: 8 }}><ArrowLeft size={14} />Danh sách đề</button>
        <h1 className="page-title">{exam.title}</h1>
        <p className="page-subtitle">Bản xem trước định dạng xuất bản</p>
        <div className="page-actions">
          <button className="btn btn-secondary" onClick={() => window.print()} id="btn-print-exam"><Printer size={14} />In / PDF</button>
          {exam.status === 'VALIDATED' && <button className="btn btn-success" onClick={onFinalize} id="btn-finalize-exam"><Lock size={14} />Chốt đề</button>}
          {exam.status === 'FINALIZED' && <span className="badge badge-green"><CheckCircle2 size={12} />Snapshot bất biến</span>}
        </div>
      </div>

      <div className="exam-paper" style={{ background: 'white', maxWidth: 800, margin: '0 auto', padding: '48px 56px', borderRadius: 8, boxShadow: '0 10px 40px rgba(15,23,42,.08)', border: '1px solid var(--gray-200)', fontFamily: '"Times New Roman", serif', color: '#111' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 14, marginBottom: 24 }}>
          <div style={{ textAlign: 'center' }}><div style={{ fontWeight: 700 }}>SỞ GD&amp;ĐT</div><div>TRƯỜNG THPT ………</div></div>
          <div style={{ textAlign: 'center' }}><div style={{ fontWeight: 700 }}>{exam.title.toUpperCase()}</div><div>Thời gian: {exam.durationMinutes} phút</div></div>
        </div>
        <div style={{ fontSize: 14, marginBottom: 24 }}>Họ tên: ……………………………… Số báo danh: ………………</div>
        {exam.questions.map((q, i) => (
          <div key={q.id} style={{ marginBottom: 18, fontSize: 15, lineHeight: 1.6 }}>
            <div><strong>Câu {i + 1}.</strong> {q.content} <span style={{ fontSize: 12, color: '#666' }}>({q.points}đ)</span></div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', paddingLeft: 20 }}>
              {q.options.map((o, j) => <div key={j}><strong>{String.fromCharCode(65 + j)}.</strong> {o}</div>)}
            </div>
          </div>
        ))}
        <div style={{ textAlign: 'center', marginTop: 32, fontStyle: 'italic', fontSize: 13 }}>— HẾT —</div>
        <div style={{ fontSize: 10.5, color: '#999', marginTop: 24, borderTop: '1px solid #eee', paddingTop: 8 }}>
          {exam.id} · {exam.questions.map(q => `${q.id}@r${q.revision}`).join(', ')}
        </div>
      </div>
      <style>{`@media print { .no-print, .sidebar, .topbar { display: none !important; } .exam-paper { box-shadow: none !important; border: none !important; } }`}</style>
    </div>
  );
}
