'use client';
import React, { useEffect, useState } from 'react';
import { useAuth } from '@/contexts/AuthContext';

type Blueprint = { id: string; name: string; version: string };
type Result = { id: string; status: string; items: { questionId: string; revision: number; points: number; slotIndex: number }[]; gaps: { slotIndex: number; missingCount: number; missingPoints: number }[]; job: { status: string; safeMessage?: string } | null };
const labels: Record<string,string> = { QUEUED: 'Đang chờ xử lý', PARTIAL: 'Chưa đủ câu hỏi', NEEDS_REVIEW: 'Đủ câu hỏi — chờ duyệt đề' };

export default function GeneratePage() {
  const { apiFetch, csrfToken } = useAuth();
  const [blueprints, setBlueprints] = useState<Blueprint[]>([]);
  const [blueprintId, setBlueprintId] = useState('');
  const [seed, setSeed] = useState(1);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<Result | null>(null);
  const [examId, setExamId] = useState<string | null>(null);
  const [key, setKey] = useState<string | null>(null);
  const [refresh, setRefresh] = useState(0);
  useEffect(() => {
    let active = true;
    async function load() {
      try {
        const all: Blueprint[] = [];
        for (let page = 1; ; page++) {
          const response = await apiFetch(`/api/v1/blueprints?page=${page}&pageSize=100`);
          if (!response.ok) throw new Error('Không tải được ma trận.');
          const data = await response.json();
          all.push(...data.items);
          if (all.length >= data.total || data.items.length === 0) break;
        }
        if (active) setBlueprints(all);
      } catch { if (active) setError('Không tải được ma trận. Hãy tải lại trang.'); }
    }
    void load(); return () => { active = false; };
  }, [apiFetch]);
  useEffect(() => {
    if (!examId) return;
    let active = true;
    let timer: ReturnType<typeof setTimeout>;
    async function poll() {
      try {
        const response = await apiFetch(`/api/v1/exams/${examId}/selection`);
        if (!response.ok) throw new Error('Không đọc được trạng thái tạo đề.');
        const data: Result = await response.json();
        if (!active) return;
        setResult(data);
        const terminal = data.status !== 'QUEUED' || ['Failed','Cancelled'].includes(data.job?.status ?? '');
        if (terminal) { setBusy(false); return; }
        timer = setTimeout(poll, 2000);
      } catch { if (active) { setError('Không đọc được tiến độ. Dùng nút Kiểm tra lại để tiếp tục.'); setBusy(false); } }
    }
    void poll(); return () => { active = false; clearTimeout(timer); };
  }, [apiFetch, examId, refresh]);
  async function generate(e: React.FormEvent) {
    e.preventDefault();
    const blueprint = blueprints.find(b => b.id === blueprintId);
    if (!blueprint || !csrfToken || busy) return;
    const requestKey = key ?? crypto.randomUUID();
    setKey(requestKey); setBusy(true); setError(''); setResult(null);
    try {
      const response = await apiFetch('/api/v1/exams/generate', { method:'POST', headers:{'Content-Type':'application/json','X-CSRF-TOKEN':csrfToken,'Idempotency-Key':requestKey}, body:JSON.stringify({blueprintId,blueprintVersion:blueprint.version,seed}) });
      if (!response.ok) throw new Error(response.status === 409 ? 'Ma trận đã thay đổi hoặc được lưu trữ. Hãy tải lại trang để chọn bản mới.' : 'Không tạo được yêu cầu. Bạn có thể thử lại với cùng nội dung.');
      const data = await response.json();
      setExamId(data.id);
      setRefresh(n => n + 1);
    } catch(e) { setError(e instanceof Error ? e.message : 'Không kết nối được máy chủ.'); setBusy(false); }
  }
  return <div>
    <div className="page-header"><h1 className="page-title">Tạo đề từ câu đã duyệt</h1><p className="page-subtitle">Chọn câu trong cùng bộ môn và trường; các ô thiếu được báo riêng để xử lý tiếp.</p></div>
    {error && <p role="alert">{error}</p>}
    <form onSubmit={generate} className="card" style={{padding:20,display:'grid',gap:16}}>
      <label>Ma trận<select aria-label="Ma trận" className="form-select" required disabled={busy} value={blueprintId} onChange={e=>{setBlueprintId(e.target.value);setKey(null);setExamId(null);setResult(null);}}><option value="">Chọn ma trận đang dùng</option>{blueprints.map(b=><option key={b.id} value={b.id}>{b.name}</option>)}</select></label>
      <label>Mã lựa chọn lặp lại<input className="form-input" type="number" min={-2147483648} max={2147483647} required disabled={busy} value={seed} onChange={e=>{setSeed(Number(e.target.value));setKey(null);setExamId(null);setResult(null);}} /></label>
      <button className="btn btn-primary" disabled={busy || !blueprintId || !csrfToken}>{busy ? 'Đang xử lý…' : 'Chọn câu đã duyệt'}</button>
    </form>
    {examId && <p>Mã yêu cầu: {examId} <button className="btn btn-secondary" disabled={busy} onClick={()=>{setRefresh(n=>n+1);setError('');}}>Kiểm tra lại</button></p>}
    {result && <section className="card" style={{padding:20,marginTop:16}}>
      <h2 role="status">{result.status === 'QUEUED' && result.job?.status === 'Failed' ? 'Tạo đề thất bại' : result.status === 'QUEUED' && result.job?.status === 'Cancelled' ? 'Đã hủy tạo đề' : labels[result.status] ?? result.status}</h2>
      {result.job?.safeMessage && <p role="alert">{result.job.safeMessage}</p>}
      <p>Đã chọn {result.items.length} câu · {result.items.reduce((sum,q)=>sum+q.points,0)} điểm</p>
      {result.gaps.length > 0 && <table><caption>Các ô còn thiếu</caption><thead><tr><th>Ô</th><th>Số câu thiếu</th><th>Điểm thiếu</th></tr></thead><tbody>{result.gaps.map(g=><tr key={g.slotIndex}><td>{g.slotIndex+1}</td><td>{g.missingCount}</td><td>{g.missingPoints}</td></tr>)}</tbody></table>}
      {result.status === 'NEEDS_REVIEW' && <p>Đề chưa được chốt hoặc phát hành.</p>}
      {!busy && <button className="btn btn-secondary" onClick={()=>{setKey(null);setExamId(null);setResult(null);setError('');}}>Chuẩn bị yêu cầu mới</button>}
    </section>}
  </div>;
}
