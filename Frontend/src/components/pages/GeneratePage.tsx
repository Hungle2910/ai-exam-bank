'use client';

import React, { useState } from 'react';
import { Cpu, Sparkles, Check, X, Pencil, BookMarked, Loader2, AlertTriangle } from 'lucide-react';
import clsx from 'clsx';
import { useToast } from '@/contexts/ToastContext';

/* ============================================================
   AI GENERATION (Bedrock RAG) — UI with simulated job
   ============================================================ */

type Difficulty = 'EASY' | 'MEDIUM' | 'HARD';
const DIFF: Record<Difficulty, [string, string]> = { EASY: ['Dễ', 'badge-green'], MEDIUM: ['Trung bình', 'badge-amber'], HARD: ['Khó', 'badge-red'] };

interface MissingSlot { id: string; blueprint: string; topic: string; difficulty: Difficulty; need: number }
interface Suggestion {
  id: string; slotId: string; content: string; options: string[]; correctIndex: number;
  citation: { source: string; page: string; excerpt: string };
  decision?: 'ACCEPTED' | 'DISCARDED';
}

const SLOTS: MissingSlot[] = [
  { id: 's2', blueprint: 'Giữa kỳ I – Toán 12', topic: 'Đạo hàm', difficulty: 'MEDIUM', need: 2 },
  { id: 's4', blueprint: 'Giữa kỳ I – Toán 12', topic: 'Logarit', difficulty: 'HARD', need: 5 },
];

const TEMPLATES: Omit<Suggestion, 'id' | 'slotId'>[] = [
  { content: 'Hàm số y = x⁴ − 2x² có bao nhiêu điểm cực trị?', options: ['1', '2', '3', '0'], correctIndex: 2, citation: { source: 'SGK Giải tích 12', page: 'tr. 13', excerpt: 'Điểm cực trị là nghiệm của y′ = 0 mà y′ đổi dấu…' } },
  { content: 'Giá trị lớn nhất của y = −x² + 4x trên [0; 3] là?', options: ['3', '4', '0', '5'], correctIndex: 1, citation: { source: 'SGK Giải tích 12', page: 'tr. 20', excerpt: 'GTLN trên đoạn đạt tại điểm tới hạn hoặc đầu mút…' } },
  { content: 'Nghiệm của phương trình log₃(2x − 1) = 2 là?', options: ['x = 5', 'x = 4', 'x = 4,5', 'x = 3'], correctIndex: 0, citation: { source: 'SGK Giải tích 12', page: 'tr. 82', excerpt: 'logₐ b = c ⇔ b = aᶜ với a > 0, a ≠ 1…' } },
];

export default function GeneratePage() {
  const { success } = useToast();
  const [selected, setSelected] = useState<string[]>(SLOTS.map(s => s.id));
  const [running, setRunning] = useState(false);
  const [progress, setProgress] = useState(0);
  const [suggestions, setSuggestions] = useState<Suggestion[]>([]);
  const [editingId, setEditingId] = useState<string | null>(null);

  const run = () => {
    setRunning(true); setProgress(0); setSuggestions([]);
    const t = setInterval(() => setProgress(p => {
      if (p >= 100) {
        clearInterval(t); setRunning(false);
        const out: Suggestion[] = [];
        selected.forEach(sid => {
          const slot = SLOTS.find(s => s.id === sid)!;
          for (let i = 0; i < Math.min(slot.need, 3); i++) out.push({ ...TEMPLATES[(out.length) % TEMPLATES.length], id: `${sid}-${i}`, slotId: sid });
        });
        setSuggestions(out);
        return 100;
      }
      return p + 5;
    }), 100);
  };

  const decide = (id: string, d: Suggestion['decision']) => setSuggestions(p => p.map(s => s.id === id ? { ...s, decision: d } : s));
  const accepted = suggestions.filter(s => s.decision === 'ACCEPTED').length;
  const pending = suggestions.filter(s => !s.decision).length;
  const phase = running ? ['Truy xuất tài liệu (Knowledge Base)…', 'Sinh câu hỏi với Bedrock…', 'Kiểm tra trùng lặp…'][Math.min(2, Math.floor(progress / 34))] : '';

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Tạo đề AI</h1>
        <p className="page-subtitle">Bedrock RAG gợi ý câu hỏi cho slot còn thiếu — giáo viên luôn là người quyết định</p>
      </div>

      <div style={{ background: 'linear-gradient(135deg, #f5f3ff, var(--brand-50))', border: '1px solid #ddd6fe', borderRadius: 12, padding: '12px 16px', marginBottom: 20, display: 'flex', gap: 10, alignItems: 'center', fontSize: 13, color: '#5b21b6' }}>
        <Sparkles size={16} />AI chỉ tạo <strong>bản nháp</strong>. Câu được chấp nhận sẽ vào hàng chờ duyệt như câu hỏi thông thường.
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '320px 1fr', gap: 20, alignItems: 'start' }} className="gen-grid">
        <div className="card" style={{ padding: 20 }}>
          <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--gray-500)', textTransform: 'uppercase', letterSpacing: '.06em', marginBottom: 12 }}>Slot đang thiếu</div>
          <div style={{ display: 'grid', gap: 8, marginBottom: 16 }}>
            {SLOTS.map(s => (
              <label key={s.id} style={{ display: 'flex', gap: 10, padding: 12, borderRadius: 10, cursor: 'pointer', border: `1px solid ${selected.includes(s.id) ? 'var(--brand-500)' : 'var(--gray-200)'}`, background: selected.includes(s.id) ? 'var(--brand-50)' : 'white' }}>
                <input type="checkbox" checked={selected.includes(s.id)} disabled={running} id={`slot-select-${s.id}`}
                  onChange={e => setSelected(p => e.target.checked ? [...p, s.id] : p.filter(x => x !== s.id))} />
                <div>
                  <div style={{ fontWeight: 600, fontSize: 13.5 }}>{s.topic} <span className={clsx('badge', DIFF[s.difficulty][1])}>{DIFF[s.difficulty][0]}</span></div>
                  <div style={{ fontSize: 12, color: 'var(--gray-500)' }}>{s.blueprint} · thiếu <strong style={{ color: 'var(--warning-600)' }}>{s.need}</strong> câu</div>
                </div>
              </label>
            ))}
          </div>
          <button className={clsx('btn btn-primary')} style={{ width: '100%', background: 'linear-gradient(135deg, #7c3aed, var(--brand-600))' }} disabled={running || selected.length === 0} onClick={run} id="btn-run-generate">
            {running ? <Loader2 size={14} className="spin" /> : <Cpu size={14} />}{running ? 'Đang tạo…' : 'Tạo gợi ý bằng AI'}
          </button>
          {running && (
            <div style={{ marginTop: 14 }}>
              <div style={{ height: 6, background: 'var(--gray-100)', borderRadius: 99, overflow: 'hidden' }}>
                <div style={{ width: `${progress}%`, height: '100%', background: 'linear-gradient(90deg, #7c3aed, var(--brand-500))', transition: 'width .1s' }} />
              </div>
              <div style={{ fontSize: 12, color: 'var(--gray-500)', marginTop: 6 }}>{phase}</div>
            </div>
          )}
          {suggestions.length > 0 && (
            <div style={{ marginTop: 16, paddingTop: 16, borderTop: '1px solid var(--gray-100)', display: 'grid', gap: 8 }}>
              <div style={{ fontSize: 13 }}>Đã chấp nhận <strong>{accepted}</strong> · còn <strong>{pending}</strong> chờ xem</div>
              <button className="btn btn-success btn-sm" disabled={accepted === 0} id="btn-submit-accepted"
                onClick={() => { success(`Đã gửi ${accepted} câu vào hàng chờ duyệt`); setSuggestions(p => p.filter(s => s.decision !== 'ACCEPTED')); }}>
                Gửi {accepted} câu đi duyệt
              </button>
            </div>
          )}
        </div>

        <div style={{ display: 'grid', gap: 14 }}>
          {suggestions.length === 0 && !running && (
            <div className="table-container"><div className="table-empty">
              <div className="table-empty-icon"><Sparkles /></div>
              <div className="table-empty-text">Chưa có gợi ý</div>
              <div className="table-empty-sub">Chọn slot và bấm “Tạo gợi ý bằng AI”</div>
            </div></div>
          )}
          {running && [0, 1].map(i => <div key={i} className="skeleton" style={{ height: 200, borderRadius: 14 }} />)}
          {suggestions.map(s => {
            const slot = SLOTS.find(x => x.id === s.slotId)!;
            const isEditing = editingId === s.id;
            return (
              <div key={s.id} className="card" style={{ padding: 20, opacity: s.decision === 'DISCARDED' ? 0.5 : 1, transition: 'opacity .2s',
                borderLeft: `3px solid ${s.decision === 'ACCEPTED' ? 'var(--accent-600)' : s.decision === 'DISCARDED' ? 'var(--gray-300, #d1d5db)' : '#7c3aed'}` }}>
                <div style={{ display: 'flex', gap: 6, marginBottom: 10, flexWrap: 'wrap' }}>
                  <span className="badge badge-purple"><Sparkles size={11} />AI Draft</span>
                  <span className="badge badge-blue">{slot.topic}</span>
                  <span className={clsx('badge', DIFF[slot.difficulty][1])}>{DIFF[slot.difficulty][0]}</span>
                  {s.decision === 'ACCEPTED' && <span className="badge badge-green"><Check size={11} />Đã chấp nhận</span>}
                </div>
                {isEditing ? (
                  <textarea className="form-input" rows={2} value={s.content} id={`edit-content-${s.id}`} autoFocus
                    onChange={e => setSuggestions(p => p.map(x => x.id === s.id ? { ...x, content: e.target.value } : x))} style={{ marginBottom: 10 }} />
                ) : (
                  <div style={{ fontSize: 15, fontWeight: 600, color: 'var(--gray-900)', marginBottom: 10 }}>{s.content}</div>
                )}
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6, marginBottom: 12 }}>
                  {s.options.map((o, i) => (
                    <div key={i} style={{ fontSize: 13, padding: '6px 10px', borderRadius: 8, background: i === s.correctIndex ? 'var(--accent-50)' : 'var(--gray-50)', color: i === s.correctIndex ? 'var(--accent-600)' : 'var(--gray-700)', fontWeight: i === s.correctIndex ? 600 : 400 }}>
                      {String.fromCharCode(65 + i)}. {o}
                    </div>
                  ))}
                </div>
                <div style={{ background: 'var(--gray-50)', borderRadius: 8, padding: '10px 12px', fontSize: 12.5, color: 'var(--gray-600)', display: 'flex', gap: 8, marginBottom: 14 }}>
                  <BookMarked size={14} style={{ flexShrink: 0, marginTop: 2, color: 'var(--brand-600)' }} />
                  <div><strong>{s.citation.source}</strong>, {s.citation.page} — <em>“{s.citation.excerpt}”</em></div>
                </div>
                {!s.decision ? (
                  <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                    <button className="btn btn-ghost btn-sm" onClick={() => decide(s.id, 'DISCARDED')} id={`btn-discard-${s.id}`}><X size={13} />Bỏ</button>
                    <button className="btn btn-secondary btn-sm" onClick={() => setEditingId(isEditing ? null : s.id)} id={`btn-edit-${s.id}`}><Pencil size={13} />{isEditing ? 'Xong' : 'Sửa'}</button>
                    <button className="btn btn-success btn-sm" onClick={() => { decide(s.id, 'ACCEPTED'); setEditingId(null); }} id={`btn-accept-${s.id}`}><Check size={13} />Chấp nhận</button>
                  </div>
                ) : (
                  <div style={{ textAlign: 'right' }}><button className="btn btn-ghost btn-sm" onClick={() => decide(s.id, undefined)} id={`btn-undo-${s.id}`}>Hoàn tác</button></div>
                )}
              </div>
            );
          })}
          {suggestions.length > 0 && selected.some(id => (SLOTS.find(s => s.id === id)?.need ?? 0) > 3) && (
            <div style={{ display: 'flex', gap: 8, fontSize: 12.5, color: 'var(--warning-600)' }}><AlertTriangle size={14} />Mỗi lượt tạo tối đa 3 gợi ý/slot. Chạy lại để có thêm.</div>
          )}
        </div>
      </div>
      <style>{`.spin { animation: spin 1s linear infinite; } @keyframes spin { to { transform: rotate(360deg); } } @media (max-width: 1000px) { .gen-grid { grid-template-columns: 1fr !important; } }`}</style>
    </div>
  );
}
