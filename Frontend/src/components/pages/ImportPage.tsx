'use client';

import React, { useRef, useState } from 'react';
import { Upload, FileSpreadsheet, CheckCircle2, AlertTriangle, Download, X, Loader2 } from 'lucide-react';
import clsx from 'clsx';
import { useToast } from '@/contexts/ToastContext';

/* ============================================================
   CSV IMPORT — client-side parse + validation preview
   ============================================================ */

const COLUMNS = ['content', 'option_a', 'option_b', 'option_c', 'option_d', 'correct', 'subject', 'topic', 'difficulty', 'points'] as const;
const SAMPLE = `${COLUMNS.join(',')}
"Đạo hàm của y = x² là?",2x,x,2,x²,A,Toán,Đạo hàm,EASY,0.25
"Thủ đô của Việt Nam?",Hà Nội,Huế,Đà Nẵng,TP.HCM,A,Địa lý,Hành chính,EASY,0.25
"Câu thiếu đáp án",1,2,3,,E,Toán,Logarit,SUPER,abc`;

interface Row { line: number; values: Record<string, string>; errors: string[] }

function parseCsv(text: string): string[][] {
  const rows: string[][] = []; let row: string[] = []; let cell = ''; let q = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (q) { if (c === '"' && text[i + 1] === '"') { cell += '"'; i++; } else if (c === '"') q = false; else cell += c; }
    else if (c === '"') q = true;
    else if (c === ',') { row.push(cell); cell = ''; }
    else if (c === '\n' || c === '\r') { if (c === '\r' && text[i + 1] === '\n') i++; row.push(cell); rows.push(row); row = []; cell = ''; }
    else cell += c;
  }
  if (cell || row.length) { row.push(cell); rows.push(row); }
  return rows.filter(r => r.some(x => x.trim()));
}

function validate(text: string): { rows: Row[]; headerError?: string } {
  const raw = parseCsv(text.replace(/^\uFEFF/, ''));
  if (raw.length === 0) return { rows: [], headerError: 'File rỗng' };
  const header = raw[0].map(h => h.trim().toLowerCase());
  const missingCols = COLUMNS.filter(c => !header.includes(c));
  if (missingCols.length) return { rows: [], headerError: `Thiếu cột: ${missingCols.join(', ')}` };
  return {
    rows: raw.slice(1).map((r, i) => {
      const v: Record<string, string> = {};
      header.forEach((h, idx) => { v[h] = (r[idx] ?? '').trim(); });
      const e: string[] = [];
      if (!v.content) e.push('content trống');
      ['option_a', 'option_b', 'option_c', 'option_d'].forEach(o => { if (!v[o]) e.push(`${o} trống`); });
      if (!['A', 'B', 'C', 'D'].includes(v.correct?.toUpperCase())) e.push('correct phải là A–D');
      if (!v.subject) e.push('subject trống');
      if (!v.topic) e.push('topic trống');
      if (!['EASY', 'MEDIUM', 'HARD'].includes(v.difficulty?.toUpperCase())) e.push('difficulty phải là EASY/MEDIUM/HARD');
      const p = Number(v.points); if (!(p > 0)) e.push('points phải > 0');
      return { line: i + 2, values: v, errors: e };
    }),
  };
}

type Stage = 'upload' | 'preview' | 'importing' | 'done';

export default function ImportPage() {
  const { success, error: showError } = useToast();
  const inputRef = useRef<HTMLInputElement>(null);
  const [stage, setStage] = useState<Stage>('upload');
  const [fileName, setFileName] = useState('');
  const [rows, setRows] = useState<Row[]>([]);
  const [dragging, setDragging] = useState(false);
  const [onlyErrors, setOnlyErrors] = useState(false);
  const [progress, setProgress] = useState(0);

  const valid = rows.filter(r => r.errors.length === 0);
  const invalid = rows.filter(r => r.errors.length > 0);

  const handleText = (name: string, text: string) => {
    const res = validate(text);
    if (res.headerError) { showError(res.headerError); return; }
    setFileName(name); setRows(res.rows); setStage('preview');
  };

  const handleFile = (f?: File) => {
    if (!f) return;
    if (!f.name.toLowerCase().endsWith('.csv')) { showError('Chỉ chấp nhận file .csv'); return; }
    if (f.size > 5 * 1024 * 1024) { showError('File tối đa 5MB'); return; }
    f.text().then(t => handleText(f.name, t));
  };

  const commit = () => {
    setStage('importing'); setProgress(0);
    const t = setInterval(() => setProgress(p => {
      if (p >= 100) { clearInterval(t); setStage('done'); success(`Đã import ${valid.length} câu hỏi`); return 100; }
      return p + 10;
    }), 120);
  };

  const downloadTemplate = () => {
    const blob = new Blob(['\uFEFF' + SAMPLE], { type: 'text/csv;charset=utf-8' });
    const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = 'mau-import-cau-hoi.csv'; a.click(); URL.revokeObjectURL(a.href);
  };

  const reset = () => { setStage('upload'); setRows([]); setFileName(''); setOnlyErrors(false); };
  const steps = ['Tải file', 'Kiểm tra', 'Import'];
  const stepIdx = stage === 'upload' ? 0 : stage === 'preview' ? 1 : 2;

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">Import CSV</h1>
        <p className="page-subtitle">Nhập hàng loạt câu hỏi với kiểm tra lỗi từng dòng trước khi ghi</p>
        <div className="page-actions">
          <button className="btn btn-secondary" onClick={downloadTemplate} id="btn-download-template"><Download size={14} />Tải file mẫu</button>
        </div>
      </div>

      {/* Stepper */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 24 }}>
        {steps.map((s, i) => (
          <React.Fragment key={s}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ width: 26, height: 26, borderRadius: 99, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 700, transition: 'all .2s',
                background: i <= stepIdx ? 'var(--brand-600)' : 'var(--gray-100)', color: i <= stepIdx ? 'white' : 'var(--gray-400)' }}>{i < stepIdx || stage === 'done' ? '✓' : i + 1}</span>
              <span style={{ fontSize: 13, fontWeight: i === stepIdx ? 600 : 500, color: i <= stepIdx ? 'var(--gray-900)' : 'var(--gray-400)' }}>{s}</span>
            </div>
            {i < steps.length - 1 && <div style={{ flex: 1, maxWidth: 80, height: 2, background: i < stepIdx ? 'var(--brand-500)' : 'var(--gray-200)' }} />}
          </React.Fragment>
        ))}
      </div>

      {stage === 'upload' && (
        <div
          onDragOver={e => { e.preventDefault(); setDragging(true); }}
          onDragLeave={() => setDragging(false)}
          onDrop={e => { e.preventDefault(); setDragging(false); handleFile(e.dataTransfer.files[0]); }}
          onClick={() => inputRef.current?.click()}
          id="csv-dropzone"
          style={{
            border: `2px dashed ${dragging ? 'var(--brand-500)' : 'var(--gray-300, #d1d5db)'}`, borderRadius: 16, padding: '64px 24px', textAlign: 'center', cursor: 'pointer',
            background: dragging ? 'var(--brand-50)' : 'white', transition: 'all .2s',
          }}>
          <div style={{ width: 64, height: 64, borderRadius: 16, background: 'var(--brand-50)', color: 'var(--brand-600)', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 16px' }}>
            <Upload size={28} />
          </div>
          <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--gray-900)', marginBottom: 6 }}>Kéo thả file CSV vào đây</div>
          <div style={{ fontSize: 13, color: 'var(--gray-500)', marginBottom: 16 }}>hoặc bấm để chọn file · tối đa 5MB · UTF-8</div>
          <button className="btn btn-ghost btn-sm" onClick={e => { e.stopPropagation(); handleText('mau-import-cau-hoi.csv', SAMPLE); }} id="btn-try-sample">Dùng thử dữ liệu mẫu</button>
          <input ref={inputRef} type="file" accept=".csv,text/csv" hidden onChange={e => handleFile(e.target.files?.[0])} id="csv-file-input" />
        </div>
      )}

      {stage === 'preview' && (
        <>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 12, marginBottom: 16 }}>
            <SummaryCard icon={<FileSpreadsheet size={18} />} label={fileName} value={`${rows.length} dòng`} color="var(--brand-600)" />
            <SummaryCard icon={<CheckCircle2 size={18} />} label="Hợp lệ" value={valid.length} color="var(--accent-600)" />
            <SummaryCard icon={<AlertTriangle size={18} />} label="Có lỗi" value={invalid.length} color="var(--danger-600)" />
          </div>
          <div className="table-container">
            <div className="table-toolbar">
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, cursor: 'pointer' }}>
                <input type="checkbox" checked={onlyErrors} onChange={e => setOnlyErrors(e.target.checked)} id="chk-only-errors" />Chỉ hiện dòng lỗi
              </label>
              <div style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
                <button className="btn btn-secondary btn-sm" onClick={reset} id="btn-cancel-import"><X size={13} />Hủy</button>
                <button className="btn btn-primary btn-sm" disabled={valid.length === 0} onClick={commit} id="btn-commit-import">
                  Import {valid.length} dòng hợp lệ
                </button>
              </div>
            </div>
            <div style={{ overflowX: 'auto' }}>
              <table>
                <thead><tr><th>Dòng</th><th>Nội dung</th><th>Đáp án</th><th>Môn / Chủ đề</th><th>Độ khó</th><th>Điểm</th><th>Kết quả</th></tr></thead>
                <tbody>
                  {(onlyErrors ? invalid : rows).map(r => (
                    <tr key={r.line} style={r.errors.length ? { background: 'var(--danger-50)' } : undefined}>
                      <td style={{ color: 'var(--gray-400)', fontVariantNumeric: 'tabular-nums' }}>#{r.line}</td>
                      <td style={{ maxWidth: 300, fontWeight: 500 }}>{r.values.content || <em style={{ color: 'var(--gray-400)' }}>trống</em>}</td>
                      <td>{r.values.correct}</td>
                      <td>{r.values.subject} / {r.values.topic}</td>
                      <td>{r.values.difficulty}</td>
                      <td>{r.values.points}</td>
                      <td>
                        {r.errors.length === 0
                          ? <span className="badge badge-green"><CheckCircle2 size={12} />OK</span>
                          : <div style={{ display: 'grid', gap: 2 }}>{r.errors.map((e, i) => <span key={i} style={{ fontSize: 12, color: 'var(--danger-600)' }}>• {e}</span>)}</div>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}

      {(stage === 'importing' || stage === 'done') && (
        <div className="card" style={{ padding: 40, textAlign: 'center', maxWidth: 520 }}>
          <div style={{ width: 64, height: 64, borderRadius: 99, margin: '0 auto 16px', display: 'flex', alignItems: 'center', justifyContent: 'center',
            background: stage === 'done' ? 'var(--accent-50)' : 'var(--brand-50)', color: stage === 'done' ? 'var(--accent-600)' : 'var(--brand-600)' }}>
            {stage === 'done' ? <CheckCircle2 size={30} /> : <Loader2 size={30} className="spin" />}
          </div>
          <div style={{ fontSize: 17, fontWeight: 700, marginBottom: 6 }}>{stage === 'done' ? 'Import hoàn tất' : 'Đang import…'}</div>
          <div style={{ fontSize: 13, color: 'var(--gray-500)', marginBottom: 18 }}>
            {stage === 'done' ? `${valid.length} câu hỏi được tạo ở trạng thái Chờ duyệt · ${invalid.length} dòng bị bỏ qua` : `${fileName}`}
          </div>
          <div style={{ height: 8, background: 'var(--gray-100)', borderRadius: 99, overflow: 'hidden', marginBottom: 20 }}>
            <div style={{ width: `${progress}%`, height: '100%', background: 'linear-gradient(90deg, var(--brand-500), var(--brand-600))', transition: 'width .12s' }} />
          </div>
          {stage === 'done' && <button className="btn btn-primary" onClick={reset} id="btn-import-another">Import file khác</button>}
        </div>
      )}
      <style>{`.spin { animation: spin 1s linear infinite; } @keyframes spin { to { transform: rotate(360deg); } }`}</style>
    </div>
  );
}

function SummaryCard({ icon, label, value, color }: { icon: React.ReactNode; label: string; value: React.ReactNode; color: string }) {
  return (
    <div className="card" style={{ padding: 16, display: 'flex', gap: 12, alignItems: 'center' }}>
      <div style={{ color }}>{icon}</div>
      <div style={{ minWidth: 0 }}>
        <div style={{ fontSize: 18, fontWeight: 700, color: 'var(--gray-900)' }}>{value}</div>
        <div className={clsx()} style={{ fontSize: 12, color: 'var(--gray-500)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{label}</div>
      </div>
    </div>
  );
}
