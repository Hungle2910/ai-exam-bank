'use client';

import React, { useEffect, useState, useCallback } from 'react';
import { FileText, Plus, Trash2, RefreshCw, Database, ArrowLeft, Save, AlertTriangle, CheckCircle2 } from 'lucide-react';
import clsx from 'clsx';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
import { fetchOrMock, formatDateTime } from '@/lib/fetchOrMock';

/* ============================================================
   TYPES & MOCK
   ============================================================ */

type Difficulty = 'EASY' | 'MEDIUM' | 'HARD';

interface BlueprintSlot {
  id: string;
  subject: string;
  topic: string;
  difficulty: Difficulty;
  questionCount: number;
  pointsPerQuestion: number;
  available?: number; // approved questions matching this slot
}

interface Blueprint {
  id: string;
  name: string;
  subject: string;
  totalPoints: number;
  durationMinutes: number;
  updatedAt: string;
  slots: BlueprintSlot[];
}

const MOCK: Blueprint[] = [
  {
    id: 'bp-1', name: 'Kiểm tra giữa kỳ I – Toán 12', subject: 'Toán', totalPoints: 10, durationMinutes: 90, updatedAt: '2026-10-16T07:00:00Z',
    slots: [
      { id: 's1', subject: 'Toán', topic: 'Đạo hàm', difficulty: 'EASY', questionCount: 12, pointsPerQuestion: 0.2, available: 30 },
      { id: 's2', subject: 'Toán', topic: 'Đạo hàm', difficulty: 'MEDIUM', questionCount: 10, pointsPerQuestion: 0.2, available: 8 },
      { id: 's3', subject: 'Toán', topic: 'Logarit', difficulty: 'MEDIUM', questionCount: 20, pointsPerQuestion: 0.2, available: 22 },
      { id: 's4', subject: 'Toán', topic: 'Logarit', difficulty: 'HARD', questionCount: 8, pointsPerQuestion: 0.2, available: 3 },
    ],
  },
  {
    id: 'bp-2', name: 'Đề ôn tập Hóa 11 – Chương 1', subject: 'Hóa học', totalPoints: 10, durationMinutes: 45, updatedAt: '2026-10-14T03:30:00Z',
    slots: [
      { id: 's5', subject: 'Hóa học', topic: 'Bảng tuần hoàn', difficulty: 'EASY', questionCount: 20, pointsPerQuestion: 0.25, available: 40 },
      { id: 's6', subject: 'Hóa học', topic: 'Liên kết hóa học', difficulty: 'MEDIUM', questionCount: 20, pointsPerQuestion: 0.25, available: 25 },
    ],
  },
];

const DIFF: Record<Difficulty, [string, string]> = {
  EASY: ['Dễ', 'badge-green'], MEDIUM: ['Trung bình', 'badge-amber'], HARD: ['Khó', 'badge-red'],
};

const round = (n: number) => Math.round(n * 100) / 100;
const sumPoints = (s: BlueprintSlot[]) => round(s.reduce((a, x) => a + x.questionCount * x.pointsPerQuestion, 0));
const sumQuestions = (s: BlueprintSlot[]) => s.reduce((a, x) => a + x.questionCount, 0);
const missing = (s: BlueprintSlot[]) => s.filter(x => x.available !== undefined && x.available < x.questionCount);

/* ============================================================
   PAGE
   ============================================================ */

export default function BlueprintsPage() {
  const [list, setList] = useState<Blueprint[]>([]);
  const [isMock, setIsMock] = useState(false);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<Blueprint | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    const r = await fetchOrMock<Blueprint[]>('/api/v1/blueprints', MOCK);
    setList(r.data);
    setIsMock(r.isMock);
    setLoading(false);
  }, []);

  useEffect(() => { load(); }, [load]);

  if (editing) {
    return (
      <BlueprintEditor
        initial={editing}
        isMock={isMock}
        onBack={() => setEditing(null)}
        onSaved={bp => {
          setList(prev => prev.some(p => p.id === bp.id) ? prev.map(p => p.id === bp.id ? bp : p) : [bp, ...prev]);
          setEditing(null);
        }}
      />
    );
  }

  const newBlueprint = (): Blueprint => ({
    id: `bp-${Date.now()}`, name: '', subject: 'Toán', totalPoints: 10, durationMinutes: 45, updatedAt: new Date().toISOString(),
    slots: [{ id: `s-${Date.now()}`, subject: 'Toán', topic: '', difficulty: 'EASY', questionCount: 10, pointsPerQuestion: 0.25 }],
  });

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Ma trận đề</h1>
        <p className="page-subtitle">Thiết kế ràng buộc môn / chủ đề / độ khó / số câu / điểm cho đề thi</p>
        <div className="page-actions">
          {isMock && <span className="badge badge-amber"><Database size={12} />Dữ liệu mẫu</span>}
          <button className="btn btn-secondary" onClick={load} id="btn-refresh-blueprints"><RefreshCw size={14} /></button>
          <button className="btn btn-primary" onClick={() => setEditing(newBlueprint())} id="btn-create-blueprint"><Plus size={14} />Tạo ma trận</button>
        </div>
      </div>

      {loading ? (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))', gap: 16 }}>
          {[0, 1, 2].map(i => <div key={i} className="skeleton" style={{ height: 190, borderRadius: 14 }} />)}
        </div>
      ) : list.length === 0 ? (
        <div className="table-container"><div className="table-empty">
          <div className="table-empty-icon"><FileText /></div>
          <div className="table-empty-text">Chưa có ma trận đề nào</div>
          <div className="table-empty-sub">Tạo ma trận đầu tiên để bắt đầu lắp ráp đề thi</div>
        </div></div>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))', gap: 16 }}>
          {list.map(bp => {
            const pts = sumPoints(bp.slots);
            const miss = missing(bp.slots);
            const ok = pts === bp.totalPoints && miss.length === 0;
            return (
              <button key={bp.id} className="card blueprint-card" onClick={() => setEditing(bp)} id={`blueprint-${bp.id}`}
                style={{ padding: 20, textAlign: 'left', cursor: 'pointer', border: '1px solid var(--gray-200)', transition: 'all .2s' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 12, gap: 8 }}>
                  <div style={{ fontSize: 15, fontWeight: 700, color: 'var(--gray-900)', lineHeight: 1.4 }}>{bp.name}</div>
                  {ok
                    ? <span className="badge badge-green"><CheckCircle2 size={12} />Sẵn sàng</span>
                    : <span className="badge badge-amber"><AlertTriangle size={12} />{miss.length > 0 ? `Thiếu ${miss.length} slot` : 'Lệch điểm'}</span>}
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8, marginBottom: 14 }}>
                  <Mini label="Số câu" value={sumQuestions(bp.slots)} />
                  <Mini label="Tổng điểm" value={`${pts}/${bp.totalPoints}`} />
                  <Mini label="Thời gian" value={`${bp.durationMinutes}′`} />
                </div>
                <DifficultyBar slots={bp.slots} />
                <div style={{ fontSize: 11.5, color: 'var(--gray-400)', marginTop: 12 }}>
                  {bp.subject} · {bp.slots.length} slot · cập nhật {formatDateTime(bp.updatedAt)}
                </div>
              </button>
            );
          })}
        </div>
      )}
      <style>{`.blueprint-card:hover { border-color: var(--brand-300, #93c5fd) !important; box-shadow: 0 8px 24px rgba(37,99,235,.08); transform: translateY(-2px); }`}</style>
    </div>
  );
}

function Mini({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div style={{ background: 'var(--gray-50)', borderRadius: 8, padding: '8px 10px' }}>
      <div style={{ fontSize: 11, color: 'var(--gray-400)', fontWeight: 500 }}>{label}</div>
      <div style={{ fontSize: 15, fontWeight: 700, color: 'var(--gray-900)' }}>{value}</div>
    </div>
  );
}

function DifficultyBar({ slots }: { slots: BlueprintSlot[] }) {
  const total = sumQuestions(slots) || 1;
  const by = (d: Difficulty) => slots.filter(s => s.difficulty === d).reduce((a, s) => a + s.questionCount, 0);
  const colors: Record<Difficulty, string> = { EASY: '#10b981', MEDIUM: '#f59e0b', HARD: '#ef4444' };
  return (
    <div>
      <div style={{ display: 'flex', height: 6, borderRadius: 99, overflow: 'hidden', background: 'var(--gray-100)' }}>
        {(['EASY', 'MEDIUM', 'HARD'] as Difficulty[]).map(d => (
          <div key={d} style={{ width: `${(by(d) / total) * 100}%`, background: colors[d], transition: 'width .3s' }} />
        ))}
      </div>
      <div style={{ display: 'flex', gap: 12, marginTop: 6, fontSize: 11.5, color: 'var(--gray-500)' }}>
        {(['EASY', 'MEDIUM', 'HARD'] as Difficulty[]).map(d => (
          <span key={d} style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
            <span style={{ width: 7, height: 7, borderRadius: 99, background: colors[d] }} />{DIFF[d][0]} {Math.round((by(d) / total) * 100)}%
          </span>
        ))}
      </div>
    </div>
  );
}

/* ============================================================
   EDITOR
   ============================================================ */

function BlueprintEditor({ initial, isMock, onBack, onSaved }: {
  initial: Blueprint; isMock: boolean; onBack: () => void; onSaved: (bp: Blueprint) => void;
}) {
  const { csrfToken } = useAuth();
  const { success, error: showError } = useToast();
  const [bp, setBp] = useState<Blueprint>(initial);
  const [saving, setSaving] = useState(false);

  const pts = sumPoints(bp.slots);
  const miss = missing(bp.slots);
  const errors: string[] = [];
  if (!bp.name.trim()) errors.push('Chưa nhập tên ma trận');
  if (bp.slots.length === 0) errors.push('Cần ít nhất 1 slot');
  if (bp.slots.some(s => !s.topic.trim())) errors.push('Có slot chưa nhập chủ đề');
  if (bp.slots.some(s => s.questionCount <= 0 || s.pointsPerQuestion <= 0)) errors.push('Số câu và điểm phải > 0');
  if (pts !== bp.totalPoints) errors.push(`Tổng điểm slot (${pts}) khác tổng điểm đề (${bp.totalPoints})`);

  const setSlot = (id: string, patch: Partial<BlueprintSlot>) =>
    setBp(p => ({ ...p, slots: p.slots.map(s => s.id === id ? { ...s, ...patch, available: undefined } : s) }));

  const addSlot = () => setBp(p => ({
    ...p, slots: [...p.slots, { id: `s-${Date.now()}`, subject: p.subject, topic: '', difficulty: 'MEDIUM', questionCount: 5, pointsPerQuestion: 0.25 }],
  }));

  const save = async () => {
    if (errors.length) { showError(errors[0]); return; }
    setSaving(true);
    try {
      const saved = { ...bp, updatedAt: new Date().toISOString() };
      if (!isMock) {
        const isNew = !initial.name;
        const res = await fetch(isNew ? '/api/v1/blueprints' : `/api/v1/blueprints/${bp.id}`, {
          method: isNew ? 'POST' : 'PUT',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json', ...(csrfToken ? { 'X-CSRF-TOKEN': csrfToken } : {}) },
          body: JSON.stringify(saved),
        });
        if (!res.ok) { showError('Không thể lưu ma trận'); return; }
      }
      success('Đã lưu ma trận đề');
      onSaved(saved);
    } catch {
      showError('Lỗi kết nối');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div>
      <div className="page-header">
        <button className="btn btn-ghost btn-sm" onClick={onBack} id="btn-back-blueprints" style={{ marginBottom: 8 }}><ArrowLeft size={14} />Danh sách</button>
        <h1 className="page-title">{initial.name || 'Ma trận mới'}</h1>
        <p className="page-subtitle">Mỗi slot xác định số câu cần lấy từ ngân hàng câu hỏi đã duyệt</p>
        <div className="page-actions">
          <button className={clsx('btn btn-primary', saving && 'btn-loading')} disabled={saving} onClick={save} id="btn-save-blueprint">
            <Save size={14} />Lưu ma trận
          </button>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 300px', gap: 20, alignItems: 'start' }} className="bp-grid">
        <div style={{ display: 'grid', gap: 20 }}>
          <div className="card" style={{ padding: 20, display: 'grid', gridTemplateColumns: '2fr 1fr 1fr 1fr', gap: 14 }}>
            <Field label="Tên ma trận" id="bp-name">
              <input id="bp-name" className="form-input" value={bp.name} onChange={e => setBp({ ...bp, name: e.target.value })} placeholder="VD: Kiểm tra cuối kỳ I – Toán 12" />
            </Field>
            <Field label="Môn học" id="bp-subject">
              <input id="bp-subject" className="form-input" value={bp.subject} onChange={e => setBp({ ...bp, subject: e.target.value })} />
            </Field>
            <Field label="Tổng điểm" id="bp-total">
              <input id="bp-total" type="number" min={1} step={0.5} className="form-input" value={bp.totalPoints} onChange={e => setBp({ ...bp, totalPoints: Number(e.target.value) })} />
            </Field>
            <Field label="Thời gian (phút)" id="bp-duration">
              <input id="bp-duration" type="number" min={5} className="form-input" value={bp.durationMinutes} onChange={e => setBp({ ...bp, durationMinutes: Number(e.target.value) })} />
            </Field>
          </div>

          <div className="table-container">
            <div className="table-toolbar">
              <strong style={{ fontSize: 14, color: 'var(--gray-900)' }}>Slot ({bp.slots.length})</strong>
              <button className="btn btn-secondary btn-sm" style={{ marginLeft: 'auto' }} onClick={addSlot} id="btn-add-slot"><Plus size={13} />Thêm slot</button>
            </div>
            <table>
              <thead>
                <tr><th>Chủ đề</th><th style={{ width: 140 }}>Độ khó</th><th style={{ width: 100 }}>Số câu</th><th style={{ width: 110 }}>Điểm/câu</th><th style={{ width: 80 }}>Điểm</th><th style={{ width: 110 }}>Khả dụng</th><th style={{ width: 44 }}></th></tr>
              </thead>
              <tbody>
                {bp.slots.map(s => {
                  const short = s.available !== undefined && s.available < s.questionCount;
                  return (
                    <tr key={s.id} style={short ? { background: 'var(--warning-50)' } : undefined}>
                      <td><input className="form-input" value={s.topic} onChange={e => setSlot(s.id, { topic: e.target.value })} placeholder="Chủ đề" aria-label="Chủ đề" id={`slot-topic-${s.id}`} /></td>
                      <td>
                        <select className="form-select" value={s.difficulty} onChange={e => setSlot(s.id, { difficulty: e.target.value as Difficulty })} aria-label="Độ khó" id={`slot-diff-${s.id}`}>
                          {(Object.keys(DIFF) as Difficulty[]).map(d => <option key={d} value={d}>{DIFF[d][0]}</option>)}
                        </select>
                      </td>
                      <td><input type="number" min={1} className="form-input" value={s.questionCount} onChange={e => setSlot(s.id, { questionCount: Number(e.target.value) })} aria-label="Số câu" id={`slot-count-${s.id}`} /></td>
                      <td><input type="number" min={0.05} step={0.05} className="form-input" value={s.pointsPerQuestion} onChange={e => setSlot(s.id, { pointsPerQuestion: Number(e.target.value) })} aria-label="Điểm mỗi câu" id={`slot-points-${s.id}`} /></td>
                      <td style={{ fontWeight: 600 }}>{round(s.questionCount * s.pointsPerQuestion)}</td>
                      <td>
                        {s.available === undefined
                          ? <span className="badge badge-gray">Chưa kiểm tra</span>
                          : <span className={clsx('badge', short ? 'badge-amber' : 'badge-green')}>{s.available}/{s.questionCount}</span>}
                      </td>
                      <td>
                        <button className="topbar-icon-btn" onClick={() => setBp(p => ({ ...p, slots: p.slots.filter(x => x.id !== s.id) }))} aria-label="Xóa slot" id={`slot-del-${s.id}`}>
                          <Trash2 size={14} />
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>

        {/* Summary */}
        <div className="card" style={{ padding: 20, position: 'sticky', top: 80 }}>
          <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--gray-500)', textTransform: 'uppercase', letterSpacing: '.06em', marginBottom: 14 }}>Tóm tắt</div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginBottom: 16 }}>
            <Mini label="Số câu" value={sumQuestions(bp.slots)} />
            <Mini label="Tổng điểm" value={<span style={{ color: pts === bp.totalPoints ? 'var(--accent-600)' : 'var(--danger-600)' }}>{pts}/{bp.totalPoints}</span>} />
          </div>
          <DifficultyBar slots={bp.slots} />
          <div style={{ marginTop: 18, display: 'grid', gap: 8 }}>
            {errors.length === 0 && miss.length === 0 ? (
              <div style={{ display: 'flex', gap: 8, fontSize: 13, color: 'var(--accent-600)' }}><CheckCircle2 size={16} />Ma trận hợp lệ</div>
            ) : (
              <>
                {errors.map((e, i) => (
                  <div key={i} style={{ display: 'flex', gap: 8, fontSize: 12.5, color: 'var(--danger-600)' }}><AlertTriangle size={14} style={{ flexShrink: 0, marginTop: 1 }} />{e}</div>
                ))}
                {miss.map(s => (
                  <div key={s.id} style={{ display: 'flex', gap: 8, fontSize: 12.5, color: 'var(--warning-600)' }}>
                    <AlertTriangle size={14} style={{ flexShrink: 0, marginTop: 1 }} />
                    {s.topic} ({DIFF[s.difficulty][0]}): thiếu {s.questionCount - (s.available ?? 0)} câu — dùng Tạo đề AI để bổ sung
                  </div>
                ))}
              </>
            )}
          </div>
        </div>
      </div>
      <style>{`@media (max-width: 1100px) { .bp-grid { grid-template-columns: 1fr !important; } }`}</style>
    </div>
  );
}

function Field({ label, id, children }: { label: string; id: string; children: React.ReactNode }) {
  return (
    <div className="form-group" style={{ marginBottom: 0 }}>
      <label className="form-label" htmlFor={id}>{label}</label>
      {children}
    </div>
  );
}
