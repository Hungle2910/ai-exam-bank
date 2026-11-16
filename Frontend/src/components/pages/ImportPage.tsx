'use client';

import React, { useRef, useState } from 'react';
import {
  Upload, CheckCircle2, AlertTriangle, Download,
  X, Loader2, ArrowRight
} from 'lucide-react';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
import {
  Difficulty,
  upsertBankQuestion,
  appendLocalAudit,
} from '@/lib/workflowStore';
import {
  enqueueBackgroundJob,
  fetchJobStatus,
  isTerminalJobStatus,
  JobApiItem,
} from '@/lib/jobClient';

const COLUMNS = [
  'content',
  'option_a',
  'option_b',
  'option_c',
  'option_d',
  'correct',
  'subject',
  'topic',
  'difficulty',
  'points',
] as const;

const SAMPLE_CSV = `${COLUMNS.join(',')}
"Đạo hàm của hàm số y = 2x³ − 6x tại x = 1 bằng?",0,6,-4,2,A,Toán,Đạo hàm,MEDIUM,0.25
"Nghiệm của phương trình log₃(x − 2) = 2 là?",11,9,7,8,A,Toán,Logarit,HARD,0.5
"Dòng mẫu có lỗi để kiểm tra bộ lọc dòng lỗi",1,1,3,,E,Toán,Logarit,SUPER,abc`;

interface ValidatedCsvRow {
  line: number;
  values: Record<string, string>;
  errors: string[];
  cellErrors: Record<string, string>;
}

function parseCsv(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = '';
  let quoted = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (quoted) {
      if (c === '"' && text[i + 1] === '"') {
        cell += '"';
        i++;
      } else if (c === '"') {
        quoted = false;
      } else {
        cell += c;
      }
    } else if (c === '"') {
      quoted = true;
    } else if (c === ',') {
      row.push(cell);
      cell = '';
    } else if (c === '\n' || c === '\r') {
      if (c === '\r' && text[i + 1] === '\n') i++;
      row.push(cell);
      rows.push(row);
      row = [];
      cell = '';
    } else {
      cell += c;
    }
  }
  if (cell || row.length) {
    row.push(cell);
    rows.push(row);
  }
  return rows.filter(r => r.some(x => x.trim()));
}

function validateCsvContent(text: string): { rows: ValidatedCsvRow[]; headerError?: string } {
  const raw = parseCsv(text.replace(/^\uFEFF/, ''));
  if (raw.length === 0) return { rows: [], headerError: 'Tệp CSV rỗng' };
  const header = raw[0].map(h => h.trim().toLowerCase());
  const missingCols = COLUMNS.filter(c => !header.includes(c));
  if (missingCols.length) {
    return { rows: [], headerError: `Thiếu cột bắt buộc: ${missingCols.join(', ')}` };
  }
  return {
    rows: raw.slice(1).map((r, i) => {
      const v: Record<string, string> = {};
      header.forEach((h, idx) => {
        v[h] = (r[idx] ?? '').trim();
      });
      const e: string[] = [];
      const cellErrors: Record<string, string> = {};

      if (!v.content) {
        e.push('content trống');
        cellErrors.content = 'Thiếu nội dung câu hỏi';
      }
      const opts = ['option_a', 'option_b', 'option_c', 'option_d'].map(k => v[k] ?? '');
      ['option_a', 'option_b', 'option_c', 'option_d'].forEach(o => {
        if (!v[o]) {
          e.push(`${o} trống`);
          cellErrors.options = (cellErrors.options ? `${cellErrors.options}; ` : '') + `Cột ${o} bị trống`;
        }
      });
      const nonEmptyOpts = opts.filter(Boolean).map(x => x.toLowerCase());
      if (nonEmptyOpts.length >= 2 && new Set(nonEmptyOpts).size !== nonEmptyOpts.length) {
        e.push('Phương án A–D trùng lặp');
        cellErrors.options = (cellErrors.options ? `${cellErrors.options}; ` : '') + 'Trùng phương án';
      }
      if (!['A', 'B', 'C', 'D'].includes(v.correct?.toUpperCase())) {
        e.push('correct phải là A, B, C hoặc D');
        cellErrors.correct = `Giá trị "${v.correct || 'trống'}" sai (chỉ nhận A–D)`;
      }
      if (!v.subject) {
        e.push('subject trống');
        cellErrors.subject = 'Thiếu môn học';
      }
      if (!v.topic) {
        e.push('topic trống');
        cellErrors.topic = 'Thiếu chủ đề';
      }
      if (!['EASY', 'MEDIUM', 'HARD'].includes(v.difficulty?.toUpperCase())) {
        e.push('difficulty phải là EASY, MEDIUM hoặc HARD');
        cellErrors.difficulty = `"${v.difficulty || 'trống'}" sai (EASY/MEDIUM/HARD)`;
      }
      const p = Number(v.points);
      if (!(p > 0)) {
        e.push('points phải là số dương (> 0)');
        cellErrors.points = `"${v.points || 'trống'}" không phải số dương`;
      }
      return { line: i + 2, values: v, errors: e, cellErrors };
    }),
  };
}

type ImportStage = 'upload' | 'preview' | 'importing' | 'done';

export default function ImportPage({ onNavigate }: { onNavigate?: (href: string) => void }) {
  const { user } = useAuth();
  const { success, error: showError } = useToast();
  const inputRef = useRef<HTMLInputElement>(null);

  const [stage, setStage] = useState<ImportStage>('upload');
  const [fileName, setFileName] = useState('');
  const [rows, setRows] = useState<ValidatedCsvRow[]>([]);
  const [dragging, setDragging] = useState(false);
  const [onlyErrors, setOnlyErrors] = useState(false);
  const [submitForReview, setSubmitForReview] = useState(true);
  const [activeJob, setActiveJob] = useState<JobApiItem | null>(null);
  const [committedCount, setCommittedCount] = useState(0);

  const valid = rows.filter(r => r.errors.length === 0);
  const invalid = rows.filter(r => r.errors.length > 0);

  const handleText = (name: string, text: string) => {
    const res = validateCsvContent(text);
    if (res.headerError) {
      showError(res.headerError);
      return;
    }
    setFileName(name);
    setRows(res.rows);
    setStage('preview');
  };

  const handleFile = (f?: File) => {
    if (!f) return;
    if (!f.name.toLowerCase().endsWith('.csv')) {
      showError('Chỉ chấp nhận tệp định dạng .csv');
      return;
    }
    if (f.size > 5 * 1024 * 1024) {
      showError('Dung lượng tệp tối đa 5MB');
      return;
    }
    f.text().then(t => handleText(f.name, t));
  };

  const commitImport = async () => {
    if (stage === 'importing' || valid.length === 0) return;
    setStage('importing');
    const actor = user?.userName ?? 'gv.nguyen.van.a';
    const idempotencyKey = `csv-${fileName}-${valid.length}-${Date.now()}`;

    const { job } = await enqueueBackgroundJob({
      jobType: 'CsvImport',
      payloadReference: `csv://${fileName}#rows=${valid.length}`,
      idempotencyKey,
    });
    setActiveJob(job);

    let current = job;
    for (let i = 0; i < 8 && !isTerminalJobStatus(current.status); i++) {
      await new Promise(r => setTimeout(r, 180));
      const next = await fetchJobStatus(current.jobId);
      if (next) {
        current = next;
        setActiveJob(next);
      }
    }

    const correctMap: Record<string, number> = { A: 0, B: 1, C: 2, D: 3 };
    valid.forEach(r => {
      upsertBankQuestion({
        content: r.values.content,
        options: [r.values.option_a, r.values.option_b, r.values.option_c, r.values.option_d],
        correctIndex: correctMap[r.values.correct.toUpperCase()] ?? 0,
        subject: r.values.subject,
        topic: r.values.topic,
        difficulty: (r.values.difficulty.toUpperCase() as Difficulty) || 'MEDIUM',
        points: Number(r.values.points) || 0.25,
        source: 'IMPORT',
        actorUserName: actor,
        submitForReview,
        note: `Nhập từ bảng CSV ${fileName} (dòng #${r.line})`,
      });
    });

    appendLocalAudit({
      actor,
      action: 'IMPORT_COMMITTED',
      target: current.resultId ?? `batch-${current.jobId.slice(0, 8)}`,
      detail: `Nhập CSV "${fileName}": ${valid.length} dòng hợp lệ, ${invalid.length} dòng lỗi bị chặn`,
      schoolName: 'Trường THPT Nguyễn Du',
      correlationId: current.jobId,
    });

    setCommittedCount(valid.length);
    setStage('done');
    success(`Đã nhập thành công ${valid.length} câu hỏi từ CSV`);
  };

  const downloadTemplate = () => {
    const blob = new Blob(['\uFEFF' + SAMPLE_CSV], { type: 'text/csv;charset=utf-8' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'mau-import-cau-hoi.csv';
    a.click();
    URL.revokeObjectURL(a.href);
  };

  const reset = () => {
    setStage('upload');
    setRows([]);
    setFileName('');
    setOnlyErrors(false);
    setActiveJob(null);
  };

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-title">Nhập bảng câu hỏi CSV &amp; Đối chiếu lỗi từng ô</h1>
          <p className="page-subtitle">
            Tải mẫu CSV chuẩn 10 cột; các lỗi định dạng được đánh dấu đỏ sát số dòng và ngay tại cột tương ứng để giáo viên biết sửa ở đâu
          </p>
        </div>
        <div className="page-actions">
          <button
            className="btn btn-secondary"
            onClick={downloadTemplate}
            id="btn-download-template"
          >
            <Download size={14} />
            Tải biểu mẫu CSV chuẩn
          </button>
        </div>
      </div>

      {stage === 'upload' && (
        <div
          onDragOver={e => {
            e.preventDefault();
            setDragging(true);
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={e => {
            e.preventDefault();
            setDragging(false);
            handleFile(e.dataTransfer.files[0]);
          }}
          onClick={() => inputRef.current?.click()}
          id="csv-dropzone"
          style={{
            border: `2px dashed ${dragging ? 'var(--brand-600)' : 'var(--rule-strong)'}`,
            borderRadius: 4,
            padding: '48px 24px',
            textAlign: 'center',
            cursor: 'pointer',
            background: dragging ? '#EBF3F1' : 'var(--paper-sheet)',
          }}
        >
          <div
            style={{
              width: 44,
              height: 44,
              borderRadius: 4,
              background: '#EFECE1',
              border: '1px solid var(--rule-base)',
              color: 'var(--brand-600)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              margin: '0 auto 12px',
            }}
          >
            <Upload size={22} />
          </div>
          <div style={{ fontFamily: 'var(--font-serif)', fontSize: 17, fontWeight: 700, color: 'var(--ink-900)', marginBottom: 4 }}>
            Đặt tệp bảng câu hỏi CSV lên bàn kiểm tra
          </div>
          <div style={{ fontSize: 13, color: 'var(--ink-600)', marginBottom: 14 }}>
            10 cột bắt buộc: <code>{COLUMNS.join(', ')}</code> · UTF-8 · Tối đa 5MB
          </div>
          <button
            type="button"
            className="btn btn-secondary btn-sm"
            onClick={e => {
              e.stopPropagation();
              handleText('mau-import-cau-hoi.csv', SAMPLE_CSV);
            }}
            id="btn-try-sample"
          >
            Nạp bảng CSV mẫu (2 dòng hợp lệ, 1 dòng lỗi nhiều cột)
          </button>
          <input
            ref={inputRef}
            type="file"
            accept=".csv,text/csv"
            hidden
            onChange={e => handleFile(e.target.files?.[0])}
            id="csv-file-input"
          />
        </div>
      )}

      {stage === 'preview' && (
        <>
          <div
            style={{
              display: 'flex',
              flexWrap: 'wrap',
              gap: '10px 24px',
              padding: '10px 16px',
              background: '#EFECE1',
              border: '1px solid var(--rule-base)',
              borderRadius: 4,
              marginBottom: 14,
              fontSize: 13,
              alignItems: 'center',
            }}
          >
            <div>
              Tệp đang mở: <strong>{fileName}</strong> (<span className="mono-num">{rows.length}</span> dòng)
            </div>
            <div>
              Đạt chuẩn:{' '}
              <strong className="mono-num" style={{ color: 'var(--brand-700)' }}>
                {valid.length} dòng
              </strong>
            </div>
            <div>
              Có lỗi cần sửa:{' '}
              <strong className="mono-num" style={{ color: 'var(--danger-500)' }}>
                {invalid.length} dòng
              </strong>
            </div>
          </div>

          <div className="table-container">
            <div className="table-toolbar" style={{ flexWrap: 'wrap', gap: 12 }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={onlyErrors}
                  onChange={e => setOnlyErrors(e.target.checked)}
                  id="chk-only-errors"
                />
                Chỉ hiện dòng lỗi ({invalid.length})
              </label>

              <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={submitForReview}
                  onChange={e => setSubmitForReview(e.target.checked)}
                  id="chk-submit-review-on-import"
                />
                Gửi thẳng vào Bàn duyệt câu hỏi sau khi nhập
              </label>

              <div style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
                <button className="btn btn-secondary btn-sm" onClick={reset} id="btn-cancel-import">
                  <X size={13} />
                  Đóng tệp
                </button>
                <button
                  className="btn btn-primary btn-sm"
                  disabled={valid.length === 0}
                  onClick={() => void commitImport()}
                  id="btn-commit-import"
                >
                  Xác nhận nhập {valid.length} dòng hợp lệ
                </button>
              </div>
            </div>

            <div style={{ overflowX: 'auto' }}>
              <table>
                <thead>
                  <tr>
                    <th style={{ width: 95 }}>Dòng &amp; Trạng thái</th>
                    <th>Cột `content` (Câu dẫn)</th>
                    <th>Cột `option_a..d` (Phương án)</th>
                    <th>Cột `correct`</th>
                    <th>Cột `subject / topic`</th>
                    <th>Cột `difficulty`</th>
                    <th>Cột `points`</th>
                  </tr>
                </thead>
                <tbody>
                  {(onlyErrors ? invalid : rows).map(r => {
                    const hasRowErr = r.errors.length > 0;
                    return (
                      <tr
                        key={r.line}
                        style={hasRowErr ? { background: '#FCF3F1' } : undefined}
                      >
                        <td style={{ verticalAlign: 'top' }}>
                          <div className="mono-num" style={{ fontWeight: 700, color: 'var(--ink-900)' }}>
                            Dòng #{r.line}
                          </div>
                          {hasRowErr ? (
                            <span className="badge badge-red" style={{ marginTop: 3 }}>
                              <AlertTriangle size={11} />
                              {r.errors.length} lỗi
                            </span>
                          ) : (
                            <span className="badge badge-green" style={{ marginTop: 3 }}>
                              <CheckCircle2 size={11} />
                              Hợp lệ
                            </span>
                          )}
                        </td>

                        {/* content cell */}
                        <td style={{ maxWidth: 250, verticalAlign: 'top' }}>
                          <div style={{ fontWeight: 500 }}>
                            {r.values.content || <em style={{ color: 'var(--danger-500)' }}>(Trống)</em>}
                          </div>
                          {r.cellErrors.content && (
                            <div style={{ fontSize: 11.5, color: 'var(--danger-500)', fontWeight: 700, marginTop: 3 }}>
                              Sửa cột content: {r.cellErrors.content}
                            </div>
                          )}
                        </td>

                        {/* options cell */}
                        <td
                          style={{
                            fontSize: 12.5,
                            verticalAlign: 'top',
                            background: r.cellErrors.options ? 'var(--danger-50)' : undefined,
                          }}
                        >
                          <div>
                            A: {r.values.option_a || <strong style={{ color: 'var(--danger-500)' }}>[Thiếu]</strong>} ·{' '}
                            B: {r.values.option_b || <strong style={{ color: 'var(--danger-500)' }}>[Thiếu]</strong>} ·{' '}
                            C: {r.values.option_c || <strong style={{ color: 'var(--danger-500)' }}>[Thiếu]</strong>} ·{' '}
                            D: {r.values.option_d || <strong style={{ color: 'var(--danger-500)' }}>[Thiếu]</strong>}
                          </div>
                          {r.cellErrors.options && (
                            <div style={{ fontSize: 11.5, color: 'var(--danger-500)', fontWeight: 700, marginTop: 3 }}>
                              {r.cellErrors.options}
                            </div>
                          )}
                        </td>

                        {/* correct cell */}
                        <td
                          style={{
                            verticalAlign: 'top',
                            background: r.cellErrors.correct ? 'var(--danger-50)' : undefined,
                          }}
                        >
                          <strong className="mono-num">{r.values.correct || '—'}</strong>
                          {r.cellErrors.correct && (
                            <div style={{ fontSize: 11.5, color: 'var(--danger-500)', fontWeight: 700, marginTop: 3 }}>
                              {r.cellErrors.correct}
                            </div>
                          )}
                        </td>

                        {/* subject / topic cell */}
                        <td style={{ verticalAlign: 'top' }}>
                          <div>
                            {r.values.subject || '—'} / {r.values.topic || '—'}
                          </div>
                          {(r.cellErrors.subject || r.cellErrors.topic) && (
                            <div style={{ fontSize: 11.5, color: 'var(--danger-500)', fontWeight: 700, marginTop: 3 }}>
                              {r.cellErrors.subject || r.cellErrors.topic}
                            </div>
                          )}
                        </td>

                        {/* difficulty cell */}
                        <td
                          style={{
                            verticalAlign: 'top',
                            background: r.cellErrors.difficulty ? 'var(--danger-50)' : undefined,
                          }}
                        >
                          <span className="mono-num">{r.values.difficulty || '—'}</span>
                          {r.cellErrors.difficulty && (
                            <div style={{ fontSize: 11.5, color: 'var(--danger-500)', fontWeight: 700, marginTop: 3 }}>
                              {r.cellErrors.difficulty}
                            </div>
                          )}
                        </td>

                        {/* points cell */}
                        <td
                          style={{
                            verticalAlign: 'top',
                            background: r.cellErrors.points ? 'var(--danger-50)' : undefined,
                          }}
                        >
                          <span className="mono-num">{r.values.points || '—'}</span>
                          {r.cellErrors.points && (
                            <div style={{ fontSize: 11.5, color: 'var(--danger-500)', fontWeight: 700, marginTop: 3 }}>
                              {r.cellErrors.points}
                            </div>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}

      {(stage === 'importing' || stage === 'done') && (
        <div className="card" style={{ padding: 28, maxWidth: 540 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
            {stage === 'done' ? (
              <CheckCircle2 size={22} style={{ color: 'var(--brand-600)' }} />
            ) : (
              <Loader2 size={22} className="spin" style={{ color: 'var(--brand-600)' }} />
            )}
            <div style={{ fontFamily: 'var(--font-serif)', fontSize: 18, fontWeight: 700 }}>
              {stage === 'done' ? 'Đã ghi nhận lô câu hỏi CSV vào mục lục' : 'Đang kiểm tra & ghi nhận dữ liệu CSV…'}
            </div>
          </div>

          <div style={{ fontSize: 13.5, color: 'var(--ink-700)', marginBottom: 14 }}>
            {stage === 'done'
              ? `Kết quả: ${committedCount} dòng hợp lệ đã được tạo (${
                  submitForReview ? 'Chờ duyệt' : 'Bản nháp'
                }) · ${invalid.length} dòng lỗi bị bỏ qua.`
              : `Đang xử lý tệp ${fileName}`}
          </div>

          {activeJob && (
            <div className="margin-note" style={{ marginBottom: 14 }}>
              Mã tác vụ nền: <code>{activeJob.jobId.slice(0, 14)}</code> · Trạng thái:{' '}
              <strong>{activeJob.status}</strong>
            </div>
          )}

          <div className="progress-bar" style={{ marginBottom: 16 }}>
            <div
              className="progress-fill"
              style={{
                width: `${stage === 'done' ? 100 : activeJob?.progressPercentage ?? 50}%`,
              }}
            />
          </div>

          {stage === 'done' && (
            <div style={{ display: 'flex', gap: 8 }}>
              <button className="btn btn-secondary" onClick={reset} id="btn-import-another">
                Nhập tệp CSV khác
              </button>
              {onNavigate && (
                <button
                  className="btn btn-primary"
                  onClick={() => onNavigate(submitForReview ? '/review' : '/questions')}
                  id="btn-goto-after-import"
                >
                  {submitForReview ? 'Sang Bàn duyệt câu hỏi' : 'Xem trong Mục lục câu hỏi'}{' '}
                  <ArrowRight size={14} />
                </button>
              )}
            </div>
          )}
        </div>
      )}
      <style>{`.spin { animation: spin 1s linear infinite; } @keyframes spin { to { transform: rotate(360deg); } }`}</style>
    </div>
  );
}
