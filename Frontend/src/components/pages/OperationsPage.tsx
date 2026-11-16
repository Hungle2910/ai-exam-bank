'use client';

import React, { useState, useEffect, useCallback } from 'react';
import {
  Activity, RefreshCw, Play, RotateCcw, XCircle, CheckCircle2,
  AlertTriangle, Clock, Server, Database, ShieldAlert, Layers
} from 'lucide-react';
import clsx from 'clsx';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
import { formatDateTime } from '@/lib/fetchOrMock';
import {
  JobApiItem,
  JobAttemptItem,
  JobLifecycleStatus,
  enqueueBackgroundJob,
  fetchJobAttempts,
  fetchJobStatus,
  fetchScopedJobs,
  isTerminalJobStatus,
  retryBackgroundJob,
  cancelBackgroundJob,
} from '@/lib/jobClient';

interface ComponentHealthItem {
  name: string;
  status: string;
  checkedAt: string | null;
}

interface HealthSnapshotData {
  status: string;
  checkedAt: string | null;
  environment: string;
  deployment: unknown;
  components: ComponentHealthItem[];
  traceId: string;
}

const JOB_STATUS_BADGE: Record<JobLifecycleStatus, { label: string; badge: string }> = {
  Pending: { label: 'Chờ xử lý (Pending)', badge: 'badge-gray' },
  Running: { label: 'Đang chạy (Running)', badge: 'badge-blue' },
  Completed: { label: 'Hoàn tất (Completed)', badge: 'badge-green' },
  Failed: { label: 'Thất bại (Failed)', badge: 'badge-red' },
  Cancelled: { label: 'Đã hủy (Cancelled)', badge: 'badge-amber' },
};

const SCENARIOS = [
  { label: 'ExamGeneration — Bình thường (Ma trận chuẩn)', jobType: 'ExamGeneration' as const, payload: 'blueprint:bp-1:standard' },
  { label: 'CsvImport — Bình thường ( Lô 25 câu hỏi)', jobType: 'CsvImport' as const, payload: 's3://imports/batch-2026-10.csv' },
  { label: 'M5 Drill — Bedrock 429 Throttled (Cho phép Retry)', jobType: 'ExamGeneration' as const, payload: 'blueprint:bp-1:simulate-bedrock-throttled' },
  { label: 'M5 Drill — Timeout sau khi ghi (Check-before-write)', jobType: 'ExamGeneration' as const, payload: 'blueprint:bp-1:simulate-timeout-after-write' },
  { label: 'M5 Drill — Lỗi dòng CSV (Không tự retry)', jobType: 'CsvImport' as const, payload: 's3://imports/simulate-row-error.csv' },
];

export default function OperationsPage() {
  const { primaryRole } = useAuth();
  const { success, error: showError } = useToast();

  // Health state
  const [liveStatus, setLiveStatus] = useState<'ALIVE' | 'OFFLINE' | 'CHECKING'>('CHECKING');
  const [dbStatus, setDbStatus] = useState<'CONNECTED' | 'UNAVAILABLE' | 'CHECKING'>('CHECKING');
  const [infraProtectedCode, setInfraProtectedCode] = useState<number | null>(null);
  const [snapshot, setSnapshot] = useState<HealthSnapshotData | null>(null);
  const [loadingHealth, setLoadingHealth] = useState(true);

  // Jobs state
  const [jobs, setJobs] = useState<JobApiItem[]>([]);
  const [isLiveJobApi, setIsLiveJobApi] = useState(false);
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [selectedJobId, setSelectedJobId] = useState<string | null>(null);
  const [attempts, setAttempts] = useState<JobAttemptItem[]>([]);
  const [loadingJobs, setLoadingJobs] = useState(true);
  const [enqueuing, setEnqueuing] = useState(false);
  const [scenarioIdx, setScenarioIdx] = useState(0);
  const [actionBusyId, setActionBusyId] = useState<string | null>(null);

  const checkHealth = useCallback(async () => {
    setLoadingHealth(true);
    try {
      const liveRes = await fetch('/health/live', { cache: 'no-store' });
      setLiveStatus(liveRes.ok ? 'ALIVE' : 'OFFLINE');
    } catch {
      setLiveStatus('OFFLINE');
    }

    try {
      const dbRes = await fetch('/health/db', { cache: 'no-store' });
      setDbStatus(dbRes.ok ? 'CONNECTED' : 'UNAVAILABLE');
    } catch {
      setDbStatus('UNAVAILABLE');
    }

    try {
      const adminRes = await fetch('/api/v1/admin/infrastructure/health', {
        credentials: 'include',
        cache: 'no-store',
      });
      setInfraProtectedCode(adminRes.status);
      if (adminRes.ok) {
        setSnapshot((await adminRes.json()) as HealthSnapshotData);
        setLoadingHealth(false);
        return;
      }
    } catch {
      setInfraProtectedCode(0);
    }

    // Try opt-in local preview snapshot if enabled
    try {
      const previewRes = await fetch('/dev/infrastructure-health/snapshot', { cache: 'no-store' });
      if (previewRes.ok) {
        setSnapshot((await previewRes.json()) as HealthSnapshotData);
      } else {
        setSnapshot({
          status: 'UNKNOWN',
          checkedAt: null,
          environment: 'development (local)',
          deployment: null,
          components: [
            { name: 'database', status: 'UNKNOWN', checkedAt: null },
            { name: 'storage', status: 'UNKNOWN', checkedAt: null },
            { name: 'ai', status: 'UNKNOWN', checkedAt: null },
          ],
          traceId: 'local-trace-unverified',
        });
      }
    } catch {
      setSnapshot({
        status: 'UNKNOWN',
        checkedAt: null,
        environment: 'offline',
        deployment: null,
        components: [
          { name: 'database', status: 'UNKNOWN', checkedAt: null },
          { name: 'storage', status: 'UNKNOWN', checkedAt: null },
          { name: 'ai', status: 'UNKNOWN', checkedAt: null },
        ],
        traceId: 'offline-unverified',
      });
    } finally {
      setLoadingHealth(false);
    }
  }, []);

  const loadJobs = useCallback(async () => {
    setLoadingJobs(true);
    const res = await fetchScopedJobs(statusFilter || undefined);
    setJobs(res.jobs);
    setIsLiveJobApi(res.isLiveApi);
    setLoadingJobs(false);
  }, [statusFilter]);

  useEffect(() => {
    let active = true;
    const init = async () => {
      await checkHealth();
      if (!active) return;
      await loadJobs();
    };
    void init();
    return () => {
      active = false;
    };
  }, [checkHealth, loadJobs]);

  // Poll non-terminal jobs; stop automatically when all jobs reach terminal state
  useEffect(() => {
    const activeJobs = jobs.filter(j => !isTerminalJobStatus(j.status));
    if (activeJobs.length === 0) return;

    const timer = setInterval(async () => {
      const updates = await Promise.all(activeJobs.map(j => fetchJobStatus(j.jobId)));
      setJobs(prev =>
        prev.map(item => {
          const found = updates.find(u => u && u.jobId === item.jobId);
          return found ?? item;
        }),
      );
    }, 1200);

    return () => clearInterval(timer);
  }, [jobs]);

  const selectedJob = jobs.find(j => j.jobId === selectedJobId) ?? jobs[0] ?? null;

  useEffect(() => {
    if (!selectedJob) {
      return;
    }
    let active = true;
    fetchJobAttempts(selectedJob.jobId).then(list => {
      if (active) setAttempts(list);
    });
    return () => {
      active = false;
    };
  }, [selectedJob]);

  const handleEnqueueScenario = async () => {
    if (enqueuing) return;
    setEnqueuing(true);
    const chosen = SCENARIOS[scenarioIdx] ?? SCENARIOS[0];
    const idempotencyKey = `ops-${chosen.jobType.toLowerCase()}-${Date.now()}`;
    try {
      const { job, isLiveApi } = await enqueueBackgroundJob({
        jobType: chosen.jobType,
        payloadReference: chosen.payload,
        idempotencyKey,
      });
      setIsLiveJobApi(isLiveApi);
      setJobs(prev => [job, ...prev.filter(x => x.jobId !== job.jobId)]);
      setSelectedJobId(job.jobId);
      success(`Đã đẩy tác vụ ${job.jobType} (${job.jobId.slice(0, 12)}) vào hàng đợi`);
    } catch {
      showError('Không thể khởi tạo tác vụ nền');
    } finally {
      setEnqueuing(false);
    }
  };

  const handleRetry = async (job: JobApiItem) => {
    if (actionBusyId) return;
    setActionBusyId(job.jobId);
    try {
      const updated = await retryBackgroundJob(
        job.jobId,
        'Thử lại thủ công từ bảng điều khiển Vận hành (M5)',
      );
      if (!updated) {
        showError('Tác vụ không đủ điều kiện thử lại hoặc API từ chối (HTTP 409)');
        return;
      }
      setJobs(prev => prev.map(j => (j.jobId === updated.jobId ? updated : j)));
      const nextAttempts = await fetchJobAttempts(job.jobId);
      setAttempts(nextAttempts);
      success(`Đã gửi yêu cầu thử lại cho tác vụ ${job.jobId.slice(0, 8)}`);
    } finally {
      setActionBusyId(null);
    }
  };

  const handleCancel = async (job: JobApiItem) => {
    if (actionBusyId) return;
    setActionBusyId(job.jobId);
    try {
      const ok = await cancelBackgroundJob(job.jobId);
      if (!ok) {
        showError('Không thể hủy tác vụ đã kết thúc');
        return;
      }
      const refreshed = await fetchJobStatus(job.jobId);
      if (refreshed) {
        setJobs(prev => prev.map(j => (j.jobId === refreshed.jobId ? refreshed : j)));
      }
      success(`Đã hủy tác vụ ${job.jobId.slice(0, 8)}`);
    } finally {
      setActionBusyId(null);
    }
  };

  const isOperator = primaryRole === 'MinistryAdmin' || primaryRole === 'SchoolAdmin';

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-title">Giám sát Tiến trình nền &amp; Hạ tầng ứng dụng</h1>
          <p className="page-subtitle">
            Theo dõi vòng đời tác vụ bất đồng bộ và trạng thái kết nối dịch vụ hệ thống
          </p>
        </div>
        <div className="page-actions">
          {isLiveJobApi ? (
            <span className="badge badge-green">
              <span className="badge-dot" />
              Đã kết nối dịch vụ Job
            </span>
          ) : (
            <span className="badge badge-amber">
              <Database size={12} />
              Mô phỏng cục bộ
            </span>
          )}
          <button
            className="btn btn-secondary"
            onClick={() => {
              void checkHealth();
              void loadJobs();
            }}
            id="btn-refresh-ops"
          >
            <RefreshCw size={14} />
            Làm mới
          </button>
        </div>
      </div>

      {/* M2 Infrastructure & Liveness Panel */}
      {isOperator && (
        <div className="card" style={{ marginBottom: 20 }}>
          <div className="card-header">
            <div>
              <div className="card-title" style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <Server size={16} style={{ color: 'var(--brand-600)' }} />
                Trạng thái dịch vụ &amp; kết nối hạ tầng
              </div>
              <div className="card-subtitle">
                Kiểm tra tiến trình máy chủ ứng dụng (<code>/health/live</code>) và trạng thái kết nối cơ sở dữ liệu, lưu trữ
              </div>
            </div>
            <span className="badge badge-gray">
              Trace: {snapshot?.traceId?.slice(0, 16) ?? '—'}
            </span>
          </div>
          <div className="card-body">
            {loadingHealth ? (
              <div className="skeleton" style={{ height: 76, borderRadius: 10 }} />
            ) : (
              <>
                <div
                  style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
                    gap: 12,
                    marginBottom: 14,
                  }}
                >
                  <div style={{ padding: 12, borderRadius: 4, border: '1px solid var(--rule-base)', background: '#F7F4EB' }}>
                    <div style={{ fontSize: 12, color: 'var(--ink-600)', marginBottom: 4 }}>Tiến trình máy chủ (<code>/health/live</code>)</div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span className={clsx('badge', liveStatus === 'ALIVE' ? 'badge-green' : 'badge-red')}>
                        <span className="badge-dot" />
                        {liveStatus === 'ALIVE' ? 'ALIVE (Đang chạy)' : 'OFFLINE'}
                      </span>
                    </div>
                  </div>

                  <div style={{ padding: 12, borderRadius: 4, border: '1px solid var(--rule-base)', background: '#F7F4EB' }}>
                    <div style={{ fontSize: 12, color: 'var(--ink-600)', marginBottom: 4 }}>Cơ sở dữ liệu SQL Server (<code>/health/db</code>)</div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span className={clsx('badge', dbStatus === 'CONNECTED' ? 'badge-green' : 'badge-amber')}>
                        <span className="badge-dot" />
                        {dbStatus === 'CONNECTED' ? 'CONNECTED' : 'CHƯA KẾT NỐI / DEV-ONLY'}
                      </span>
                    </div>
                  </div>

                  <div style={{ padding: 12, borderRadius: 4, border: '1px solid var(--rule-base)', background: '#F7F4EB' }}>
                    <div style={{ fontSize: 12, color: 'var(--ink-600)', marginBottom: 4 }}>
                      Giám sát hạ tầng (<code>/api/v1/admin/infrastructure/health</code>)
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span className="badge badge-blue">
                        {infraProtectedCode === 200
                          ? '200 OK'
                          : infraProtectedCode === 401 || infraProtectedCode === 403
                          ? `Fail-closed (HTTP ${infraProtectedCode})`
                          : 'Chưa xác thực'}
                      </span>
                    </div>
                  </div>

                  {snapshot?.components.map(comp => (
                    <div
                      key={comp.name}
                      style={{ padding: 12, borderRadius: 4, border: '1px solid var(--rule-base)', background: 'var(--paper-sheet)' }}
                    >
                      <div style={{ fontSize: 12, color: 'var(--gray-500)', marginBottom: 4, textTransform: 'uppercase' }}>
                         Thành phần: {comp.name}
                      </div>
                      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <span className={clsx('badge', comp.status === 'HEALTHY' ? 'badge-green' : 'badge-gray')}>
                          {comp.status}
                        </span>
                        <span style={{ fontSize: 11.5, color: 'var(--gray-400)' }}>
                          {comp.checkedAt ? formatDateTime(comp.checkedAt) : 'Chưa kiểm chứng AWS'}
                        </span>
                      </div>
                    </div>
                  ))}
                </div>

                <div
                  style={{
                    background: 'var(--gray-50)',
                    border: '1px solid var(--gray-200)',
                    borderRadius: 8,
                    padding: '10px 14px',
                    fontSize: 12.5,
                    color: 'var(--gray-600)',
                    display: 'flex',
                    alignItems: 'center',
                    gap: 8,
                  }}
                >
                  <ShieldAlert size={15} style={{ color: 'var(--brand-600)', flexShrink: 0 }} />
                  <span>
                    <strong>Ghi chú vận hành M2:</strong> Trạng thái <code>UNKNOWN</code> thể hiện chưa có kết quả kiểm tra thực tế trên môi trường AWS/Staging. Hệ thống giữ nguyên tắc fail-closed cho tới khi claim <code>InfrastructureHealth.Read</code> được cấp.
                  </span>
                </div>
              </>
            )}
          </div>
        </div>
      )}

      {/* M5 Job Dispatcher & Filter Bar */}
      <div className="card" style={{ marginBottom: 20 }}>
        <div className="card-header">
          <div>
            <div className="card-title" style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <Layers size={16} style={{ color: 'var(--brand-600)' }} />
              Điều phối &amp; Kiểm thử tiến trình xử lý nền
            </div>
            <div className="card-subtitle">
              Theo dõi vòng đời tác vụ, khóa chống gửi lặp và cơ chế khôi phục khi gián đoạn kết nối
            </div>
          </div>
        </div>
        <div className="card-body" style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
          <select
            className="form-select"
            style={{ flex: '1 1 320px' }}
            value={scenarioIdx}
            onChange={e => setScenarioIdx(Number(e.target.value))}
            id="ops-scenario-select"
            aria-label="Chọn kịch bản tác vụ nền"
          >
            {SCENARIOS.map((s, idx) => (
              <option key={s.payload} value={idx}>
                {s.label}
              </option>
            ))}
          </select>
          <button
            className={clsx('btn btn-primary', enqueuing && 'btn-loading')}
            disabled={enqueuing}
            onClick={handleEnqueueScenario}
            id="btn-enqueue-job"
          >
            <Play size={14} />
            {enqueuing ? 'Đang gửi yêu cầu…' : 'Đẩy tác vụ vào hàng đợi'}
          </button>
        </div>
      </div>

      {/* Jobs Table + Attempt Detail Grid */}
      <div
        style={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr', gap: 20, alignItems: 'start' }}
        className="ops-grid"
      >
        <div className="table-container">
          <div className="table-toolbar" style={{ flexWrap: 'wrap', gap: 10 }}>
            <strong style={{ fontSize: 13.5, color: 'var(--gray-900)' }}>
              Danh sách tác vụ ({jobs.length})
            </strong>
            <select
              className="form-select"
              style={{ width: 190, marginLeft: 'auto' }}
              value={statusFilter}
              onChange={e => setStatusFilter(e.target.value)}
              id="ops-job-status-filter"
              aria-label="Lọc trạng thái tác vụ"
            >
              <option value="">Tất cả trạng thái</option>
              <option value="Pending">Pending (Chờ xử lý)</option>
              <option value="Running">Running (Đang chạy)</option>
              <option value="Completed">Completed (Hoàn tất)</option>
              <option value="Failed">Failed (Thất bại)</option>
              <option value="Cancelled">Cancelled (Đã hủy)</option>
            </select>
          </div>

          {loadingJobs ? (
            <div style={{ padding: 16 }}>
              {[0, 1, 2].map(i => (
                <div key={i} className="skeleton" style={{ height: 52, marginBottom: 8, borderRadius: 8 }} />
              ))}
            </div>
          ) : jobs.length === 0 ? (
            <div className="table-empty">
              <div className="table-empty-icon"><Activity /></div>
              <div className="table-empty-text">Chưa có tác vụ nền nào trong phạm vi</div>
              <div className="table-empty-sub">
                Khởi tạo tác vụ từ màn Tạo đề AI, Import CSV hoặc chọn kịch bản phía trên
              </div>
            </div>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table>
                <thead>
                  <tr>
                    <th>Mã tác vụ</th>
                    <th>Loại</th>
                    <th>Trạng thái</th>
                    <th>Tiến độ</th>
                    <th style={{ width: 110 }}>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {jobs.map(job => {
                    const meta = JOB_STATUS_BADGE[job.status] ?? JOB_STATUS_BADGE.Pending;
                    const isSelected = selectedJob?.jobId === job.jobId;
                    return (
                      <tr
                        key={job.jobId}
                        onClick={() => setSelectedJobId(job.jobId)}
                        style={{
                          cursor: 'pointer',
                          background: isSelected ? 'var(--brand-50)' : undefined,
                        }}
                      >
                        <td>
                          <code style={{ fontSize: 12, fontWeight: 600, color: 'var(--gray-800)' }}>
                            {job.jobId.slice(0, 12)}…
                          </code>
                          {job.isSimulated && (
                            <div style={{ fontSize: 11, color: 'var(--warning-600)' }}>Mô phỏng</div>
                          )}
                        </td>
                        <td style={{ fontWeight: 600, fontSize: 13 }}>{job.jobType}</td>
                        <td>
                          <span className={clsx('badge', meta.badge)}>
                            <span className="badge-dot" />
                            {meta.label}
                          </span>
                        </td>
                        <td style={{ minWidth: 90 }}>
                          {job.progressPercentage !== null && job.progressPercentage !== undefined ? (
                            <div>
                              <div className="progress-bar" style={{ height: 6 }}>
                                <div
                                  className="progress-fill"
                                  style={{
                                    width: `${job.progressPercentage}%`,
                                    background:
                                      job.status === 'Failed'
                                        ? 'var(--danger-500)'
                                        : job.status === 'Completed'
                                        ? 'var(--accent-600)'
                                        : 'var(--brand-600)',
                                  }}
                                />
                              </div>
                              <span style={{ fontSize: 11, color: 'var(--gray-500)' }}>
                                {job.progressPercentage}%
                              </span>
                            </div>
                          ) : (
                            <span style={{ fontSize: 12, color: 'var(--gray-400)' }}>Không đo</span>
                          )}
                        </td>
                        <td onClick={e => e.stopPropagation()}>
                          <div style={{ display: 'flex', gap: 6 }}>
                            {job.status === 'Failed' && job.isRetryEligible !== false && (
                              <button
                                className="btn btn-secondary btn-sm"
                                disabled={actionBusyId === job.jobId}
                                onClick={() => void handleRetry(job)}
                                id={`btn-retry-${job.jobId}`}
                                title="Thử lại tác vụ"
                              >
                                <RotateCcw size={12} />
                                Thử lại
                              </button>
                            )}
                            {!isTerminalJobStatus(job.status) && (
                              <button
                                className="btn btn-ghost btn-sm"
                                disabled={actionBusyId === job.jobId}
                                onClick={() => void handleCancel(job)}
                                id={`btn-cancel-${job.jobId}`}
                                title="Hủy tác vụ"
                              >
                                <XCircle size={13} />
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Job Detail & Attempts History */}
        <div className="card" style={{ padding: 20 }}>
          {!selectedJob ? (
            <div className="table-empty">
              <div className="table-empty-text">Chọn một tác vụ để xem chi tiết vòng đời &amp; lịch sử lượt chạy</div>
            </div>
          ) : (
            <>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8, marginBottom: 14 }}>
                <div>
                  <div style={{ fontSize: 15, fontWeight: 700, color: 'var(--gray-900)' }}>
                    Chi tiết tác vụ: {selectedJob.jobType}
                  </div>
                  <code style={{ fontSize: 12, color: 'var(--gray-500)' }}>{selectedJob.jobId}</code>
                </div>
                <span className={clsx('badge', JOB_STATUS_BADGE[selectedJob.status]?.badge ?? 'badge-gray')}>
                  {JOB_STATUS_BADGE[selectedJob.status]?.label ?? selectedJob.status}
                </span>
              </div>

              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: '1fr 1fr',
                  gap: 10,
                  padding: 12,
                  background: 'var(--gray-50)',
                  borderRadius: 10,
                  marginBottom: 16,
                  fontSize: 12.5,
                }}
              >
                <div>
                  <span style={{ color: 'var(--gray-500)' }}>Tiến độ thực tế:</span>{' '}
                  <strong>{selectedJob.progressPercentage !== null ? `${selectedJob.progressPercentage}%` : 'N/A'}</strong>
                </div>
                <div>
                  <span style={{ color: 'var(--gray-500)' }}>Mã kết quả:</span>{' '}
                  <code>{selectedJob.resultId ?? 'Chưa sinh'}</code>
                </div>
                {selectedJob.payloadReference && (
                  <div style={{ gridColumn: '1/-1' }}>
                    <span style={{ color: 'var(--gray-500)' }}>Tham chiếu đầu vào:</span>{' '}
                    <code>{selectedJob.payloadReference}</code>
                  </div>
                )}
              </div>

              {selectedJob.status === 'Failed' && (
                <div
                  style={{
                    background: 'var(--danger-50)',
                    border: '1px solid #fecaca',
                    borderRadius: 10,
                    padding: 14,
                    marginBottom: 16,
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontWeight: 700, color: 'var(--danger-600)', fontSize: 13, marginBottom: 4 }}>
                    <AlertTriangle size={15} />
                    Mã lỗi an toàn: <code>{selectedJob.errorCode ?? 'EXECUTION_ERROR'}</code>
                  </div>
                  <div style={{ fontSize: 13, color: 'var(--gray-800)', marginBottom: 6 }}>
                    {selectedJob.safeMessage ?? 'Tác vụ dừng do lỗi thực thi.'}
                  </div>
                  {selectedJob.actionableReason && (
                    <div style={{ fontSize: 12.5, color: 'var(--gray-600)', background: 'white', padding: '8px 10px', borderRadius: 6, border: '1px solid #fee2e2' }}>
                      <strong>Hướng xử lý tiếp theo:</strong> {selectedJob.actionableReason}
                    </div>
                  )}
                  <div style={{ marginTop: 10, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: 12, color: 'var(--gray-500)' }}>
                      Đủ điều kiện thử lại: <strong>{selectedJob.isRetryEligible === false ? 'Không (Lỗi dữ liệu cố định)' : 'Có (Transient/Idempotent)'}</strong>
                    </span>
                    {selectedJob.isRetryEligible !== false && (
                      <button
                        className="btn btn-primary btn-sm"
                        disabled={actionBusyId === selectedJob.jobId}
                        onClick={() => void handleRetry(selectedJob)}
                      >
                        <RotateCcw size={13} />
                        Thử lại tác vụ ngay
                      </button>
                    )}
                  </div>
                </div>
              )}

              {selectedJob.status === 'Completed' && (
                <div className="margin-note green-ink" style={{ marginBottom: 16, display: 'flex', alignItems: 'center', gap: 8 }}>
                  <CheckCircle2 size={16} />
                  <span>
                    Hoàn tất 100%, kết quả đầu ra: <strong>{selectedJob.resultId ?? 'Thành công'}</strong>
                  </span>
                </div>
              )}

              <div style={{ fontSize: 12, fontWeight: 700, color: 'var(--gray-500)', textTransform: 'uppercase', letterSpacing: '.05em', marginBottom: 10 }}>
                Lịch sử các lượt thực thi ({attempts.length})
              </div>
              {attempts.length === 0 ? (
                <div style={{ fontSize: 12.5, color: 'var(--gray-400)', padding: '12px 0' }}>
                  Chưa có lượt chạy nào được ghi nhận.
                </div>
              ) : (
                <div style={{ display: 'grid', gap: 8 }}>
                  {attempts.map(att => (
                    <div
                      key={att.id}
                      style={{
                        border: '1px solid var(--gray-200)',
                        borderRadius: 8,
                        padding: '10px 12px',
                        fontSize: 12.5,
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                        <strong>
                          Lượt #{att.attemptNumber} · {att.status}
                          {att.isManualRetry && (
                            <span className="badge badge-blue" style={{ marginLeft: 6 }}>Retry thủ công</span>
                          )}
                        </strong>
                        <span style={{ color: 'var(--gray-400)', display: 'flex', alignItems: 'center', gap: 4 }}>
                          <Clock size={11} />
                          {att.executionDurationMs !== null ? `${att.executionDurationMs}ms` : 'Đang chạy'}
                        </span>
                      </div>
                      <div style={{ color: 'var(--gray-500)', fontSize: 12 }}>
                        Worker: <code>{att.workerId}</code> · Bắt đầu: {formatDateTime(att.startedAt)}
                      </div>
                      {att.safeErrorMessage && (
                        <div style={{ color: 'var(--danger-600)', marginTop: 4 }}>
                          [{att.errorCode}] {att.safeErrorMessage}
                        </div>
                      )}
                      {att.retryReason && (
                        <div style={{ color: 'var(--gray-600)', marginTop: 4 }}>
                          Lý do thử lại: {att.retryReason}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </>
          )}
        </div>
      </div>

      <style>{`
        @media (max-width: 1024px) {
          .ops-grid { grid-template-columns: 1fr !important; }
        }
      `}</style>
    </div>
  );
}
