'use client';

import React, { useCallback, useEffect, useState } from 'react';
import { useAuth } from '@/contexts/AuthContext';

type Slot = { topicId: string; difficulty: string; questionCount: number; pointsPerQuestion: number };
type Blueprint = { id?: string; name: string; subjectId: string; durationMinutes: number; totalQuestions: number; totalPoints: number; slots: Slot[]; version?: string; archivedAt?: string | null };
type Subject = { id: string; name: string; canWrite: boolean; topics: { id: string; name: string }[] };
const empty = (): Blueprint => ({ name: '', subjectId: '', durationMinutes: 45, totalQuestions: 10, totalPoints: 10, slots: [] });

export default function BlueprintsPage() {
  const { apiFetch, csrfToken } = useAuth();
  const [items, setItems] = useState<Blueprint[]>([]);
  const [catalog, setCatalog] = useState<Subject[]>([]);
  const [editing, setEditing] = useState<Blueprint | null>(null);
  const [error, setError] = useState('');
  const [fields, setFields] = useState<Record<string, string[]>>({});
  const [busy, setBusy] = useState(true);
  const [conflict, setConflict] = useState(false);
  const [page, setPage] = useState(1);
  const [archived, setArchived] = useState(false);
  const [total, setTotal] = useState(0);
  const [notice, setNotice] = useState('');
  const load = useCallback(async () => {
    setBusy(true);
    try {
      const [list, subjects] = await Promise.all([apiFetch(`/api/v1/blueprints?page=${page}&pageSize=20&archived=${archived}`), apiFetch('/api/v1/catalog')]);
      if (!list.ok || !subjects.ok) throw new Error('Không tải được ma trận. Kiểm tra phiên đăng nhập và thử lại.');
      const data = await list.json();
      setItems(data.items); setTotal(data.total); setCatalog(await subjects.json()); setError('');
    } catch (e) { setError(e instanceof Error ? e.message : 'Không kết nối được máy chủ.'); }
    finally { setBusy(false); }
  }, [apiFetch, page, archived]);
  useEffect(() => {
    // load updates state after the network request settles, including its error path.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  function edit(value: Blueprint) { setEditing(structuredClone(value)); setFields({}); setError(''); setConflict(false); setNotice(''); }
  async function save(e: React.FormEvent) {
    e.preventDefault();
    if (!editing || editing.archivedAt || busy) return;
    if (!csrfToken) { setError('Chưa lấy được token bảo vệ phiên. Tải lại trang trước khi lưu.'); return; }
    setBusy(true); setError(''); setFields({}); setConflict(false);
    try {
      const response = await apiFetch(`/api/v1/blueprints${editing.id ? `/${editing.id}` : ''}`, {
        method: editing.id ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken }, body: JSON.stringify(editing),
      });
      if (response.status === 409) { setConflict(true); setError('Ma trận đã có phiên bản mới. Nội dung bạn đang nhập được giữ lại.'); return; }
      if (!response.ok) {
        const problem = await response.json().catch(() => null);
        setFields(problem?.errors ?? {});
        setError(response.status === 403 ? 'Bạn không có quyền sửa ma trận này.' : problem?.title ?? 'Không lưu được ma trận.');
        return;
      }
      const saved: Blueprint = await response.json();
      setEditing(saved); setNotice('Đã lưu ma trận trên máy chủ.');
    } catch { setError('Không kết nối được máy chủ. Nội dung đang nhập vẫn được giữ lại.'); }
    finally { setBusy(false); }
  }
  async function reload() {
    if (!editing?.id || !window.confirm('Tải bản mới sẽ thay nội dung chưa lưu. Tiếp tục?')) return;
    setBusy(true);
    try {
      const r = await apiFetch(`/api/v1/blueprints/${editing.id}`);
      if (!r.ok) throw new Error();
      edit(await r.json());
    } catch { setError('Không tải được bản mới. Nội dung hiện tại vẫn được giữ lại.'); }
    finally { setBusy(false); }
  }
  async function archive(value: Blueprint) {
    if (!csrfToken) { setError('Chưa lấy được token bảo vệ phiên. Tải lại trang trước khi lưu trữ.'); return; }
    if (busy || !window.confirm(`Lưu trữ ma trận “${value.name}”? Nội dung được giữ lại để xem.`)) return;
    setBusy(true); setError('');
    try {
      const result = await apiFetch(`/api/v1/blueprints/${value.id}/archive`, {
        method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken }, body: JSON.stringify({ version: value.version }),
      });
      if (!result.ok) {
        setError(result.status === 409 ? 'Ma trận đã thay đổi. Tải lại danh sách trước khi lưu trữ.' : 'Không lưu trữ được ma trận.');
        return;
      }
      setNotice('Đã lưu trữ ma trận. Nội dung được giữ lại trong mục Đã lưu trữ.');
      await load();
    } catch { setError('Không kết nối được máy chủ. Hãy tải lại để kiểm tra trạng thái trước khi thử lại.'); }
    finally { setBusy(false); }
  }
  function updateSlot(index: number, patch: Partial<Slot>) {
    if (editing) setEditing({ ...editing, slots: editing.slots.map((s, i) => i === index ? { ...s, ...patch } : s) });
  }
  const subject = catalog.find(s => s.id === editing?.subjectId);
  const field = (name: string) => fields[name] && <span role="alert" className="text-danger">{fields[name].join(' ')}</span>;
  return <div>
    <div className="page-header"><h1 className="page-title">Ma trận đề</h1><p className="page-subtitle">Thiết kế chủ đề, độ khó, số câu và điểm cho đề thi</p></div>
    {error && <div role="alert" className="card" style={{ padding: 16 }}>{error}</div>}
    {notice && <p role="status">{notice}</p>}
    {editing ? <form onSubmit={save} onChange={() => setNotice('')}>
      <fieldset disabled={busy} style={{ border: 0, padding: 0 }}>
        <div className="page-actions"><button type="button" className="btn btn-secondary" onClick={() => { if (window.confirm('Quay lại danh sách? Nội dung chưa lưu sẽ bị bỏ.')) { setEditing(null); void load(); } }}>Danh sách</button>
          <button className="btn btn-primary" disabled={!!editing.archivedAt} type="submit">{busy ? 'Đang lưu…' : 'Lưu ma trận'}</button>
          {conflict && <button className="btn btn-secondary" type="button" onClick={reload}>Tải phiên bản mới</button>}</div>
        {editing.archivedAt && <p role="status">Đã lưu trữ — chỉ xem nội dung.</p>}
        <fieldset disabled={!!editing.archivedAt} style={{ border: 0, padding: 0 }}>
        <div className="card" style={{ padding: 20, display: 'grid', gap: 16 }}>
          <label>Tên ma trận<input className="form-input" required maxLength={200} value={editing.name} onChange={e => setEditing({ ...editing, name: e.target.value })} />{field('name')}</label>
          <label>Môn học<select aria-label="Môn học" className="form-select" required disabled={!!editing.id} value={editing.subjectId} onChange={e => setEditing({ ...editing, subjectId: e.target.value, slots: [] })}>
            <option value="">Chọn môn học</option>{catalog.filter(s => s.canWrite).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>{field('subjectId')}</label>
          <label>Thời gian (phút)<input className="form-input" type="number" min={1} max={1440} required value={editing.durationMinutes} onChange={e => setEditing({ ...editing, durationMinutes: Number(e.target.value) })} />{field('durationMinutes')}</label>
          <label>Tổng số câu yêu cầu<input className="form-input" type="number" min={1} max={1000} required value={editing.totalQuestions} onChange={e => setEditing({ ...editing, totalQuestions: Number(e.target.value) })} />{field('totalQuestions')}</label>
          <label>Tổng điểm yêu cầu<input className="form-input" type="number" min={0.0001} max={10000} step="0.0001" required value={editing.totalPoints} onChange={e => setEditing({ ...editing, totalPoints: Number(e.target.value) })} />{field('totalPoints')}</label>
        </div>
        <div className="table-container" style={{ overflowX: 'auto', marginTop: 16 }}><table><caption>Ma trận chủ đề và độ khó</caption><thead><tr><th>Chủ đề</th><th>Độ khó</th><th>Số câu</th><th>Điểm/câu</th><th>Thao tác</th></tr></thead><tbody>
          {editing.slots.map((s, i) => <tr key={i}>
            <td><select aria-label={`Chủ đề ô ${i + 1}`} required value={s.topicId} onChange={e => updateSlot(i, { topicId: e.target.value })}><option value="">Chọn chủ đề</option>{subject?.topics.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}</select>{field(`slots[${i}].topicId`)}{field(`slots[${i}]`)}</td>
            <td><select aria-label={`Độ khó ô ${i + 1}`} value={s.difficulty} onChange={e => updateSlot(i, { difficulty: e.target.value })}><option value="EASY">Dễ</option><option value="MEDIUM">Trung bình</option><option value="HARD">Khó</option></select>{field(`slots[${i}].difficulty`)}</td>
            <td><input aria-label={`Số câu ô ${i + 1}`} type="number" min={1} max={1000} required value={s.questionCount} onChange={e => updateSlot(i, { questionCount: Number(e.target.value) })} />{field(`slots[${i}].questionCount`)}</td>
            <td><input aria-label={`Điểm mỗi câu ô ${i + 1}`} type="number" min={0.0001} max={10000} step="0.0001" required value={s.pointsPerQuestion} onChange={e => updateSlot(i, { pointsPerQuestion: Number(e.target.value) })} />{field(`slots[${i}].pointsPerQuestion`)}</td>
            <td><button type="button" className="btn btn-secondary" aria-label={`Xóa ô ${i + 1}`} onClick={() => setEditing({ ...editing, slots: editing.slots.filter((_, n) => n !== i) })}>Xóa ô</button></td>
          </tr>)}
        </tbody></table></div>{field('slots')}
        <p aria-live="polite">Tổng các ô: {editing.slots.reduce((n, s) => n + s.questionCount, 0)} câu · {(editing.slots.reduce((n, s) => n + s.questionCount * s.pointsPerQuestion, 0)).toFixed(4)} điểm</p>
        <button type="button" className="btn btn-secondary" disabled={!subject || editing.slots.length >= 200} onClick={() => setEditing({ ...editing, slots: [...editing.slots, { topicId: '', difficulty: 'EASY', questionCount: 1, pointsPerQuestion: 1 }] })}>Thêm ô</button>
        </fieldset>
      </fieldset>
    </form> : <>
      <div className="page-actions"><button className="btn btn-primary" disabled={busy || !catalog.some(s => s.canWrite)} onClick={() => edit(empty())}>Tạo ma trận</button><button className="btn btn-secondary" disabled={busy} onClick={load}>Tải lại</button></div>
      <label><input type="checkbox" checked={archived} disabled={busy} onChange={e => { setBusy(true); setPage(1); setArchived(e.target.checked); }} />Đã lưu trữ</label>
      {busy ? <p role="status">Đang tải…</p> : <div className="table-container"><table><thead><tr><th>Tên</th><th>Số câu</th><th>Điểm</th><th>Thao tác</th></tr></thead><tbody>{items.map(b => <tr key={b.id}><td>{b.name}</td><td>{b.totalQuestions}</td><td>{b.totalPoints}</td><td><button className="btn btn-secondary" onClick={() => edit(b)}>Mở {b.name}</button>{!archived && <button className="btn btn-secondary" disabled={busy} onClick={() => archive(b)}>Lưu trữ {b.name}</button>}</td></tr>)}</tbody></table>{items.length === 0 && <p>Chưa có ma trận trong phạm vi của bạn.</p>}</div>}
      <div className="page-actions"><button className="btn btn-secondary" disabled={busy || page === 1} onClick={() => setPage(page - 1)}>Trang trước</button><span>Trang {page} · {total} ma trận</span><button className="btn btn-secondary" disabled={busy || page * 20 >= total} onClick={() => setPage(page + 1)}>Trang sau</button></div>
    </>}
  </div>;
}
