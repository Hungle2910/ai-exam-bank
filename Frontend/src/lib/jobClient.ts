/**
 * M5 Background Job Engine API Client & M3-04 AI Draft Safety Gate validator.
 * Connects to /api/v1/jobs/* with scoped Bearer token in development,
 * and falls back to labeled local simulation only when backend is unreachable.
 */

export type JobLifecycleStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';

export interface JobApiItem {
  jobId: string;
  jobType: string;
  status: JobLifecycleStatus;
  progressPercentage: number | null;
  resultId: string | null;
  errorCode: string | null;
  safeMessage: string | null;
  isRetryEligible?: boolean | null;
  actionableReason?: string | null;
  createdAt?: string;
  payloadReference?: string;
  isSimulated?: boolean;
}

export interface JobAttemptItem {
  id: string;
  jobId: string;
  attemptNumber: number;
  status: string;
  workerId: string;
  startedAt: string;
  finishedAt: string | null;
  executionDurationMs: number | null;
  errorCode: string | null;
  safeErrorMessage: string | null;
  retryReason: string | null;
  isManualRetry: boolean;
}

export interface DevJobToken {
  token: string;
  userId: string;
  scopeId: string;
  scopeKind: 'School' | 'MinistryExam';
}

const STORAGE_JOBS_KEY = 'ai-exam-bank:tracked-jobs-v1';
let cachedDevToken: DevJobToken | null = null;

export function isTerminalJobStatus(status: JobLifecycleStatus): boolean {
  return status === 'Completed' || status === 'Failed' || status === 'Cancelled';
}

export function loadTrackedJobs(): JobApiItem[] {
  if (typeof window === 'undefined') return [];
  try {
    const raw = window.localStorage.getItem(STORAGE_JOBS_KEY);
    if (!raw) return [];
    const parsed: unknown = JSON.parse(raw);
    return Array.isArray(parsed) ? (parsed as JobApiItem[]) : [];
  } catch {
    return [];
  }
}

export function saveTrackedJobs(jobs: JobApiItem[]): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(STORAGE_JOBS_KEY, JSON.stringify(jobs.slice(0, 30)));
  } catch {
    // ignore storage quota errors
  }
}

export function upsertTrackedJob(job: JobApiItem): JobApiItem[] {
  const current = loadTrackedJobs();
  const exists = current.some(j => j.jobId === job.jobId);
  const next = exists
    ? current.map(j => (j.jobId === job.jobId ? { ...j, ...job } : j))
    : [{ ...job, createdAt: job.createdAt ?? new Date().toISOString() }, ...current];
  saveTrackedJobs(next);
  return next;
}

export async function acquireDevJobToken(
  scopeId = 'school-hn-01',
  scopeKind: 'School' | 'MinistryExam' = 'School',
): Promise<DevJobToken | null> {
  if (cachedDevToken && cachedDevToken.scopeId === scopeId && cachedDevToken.scopeKind === scopeKind) {
    return cachedDevToken;
  }
  try {
    const res = await fetch(
      `/api/v1/jobs/dev-token?scopeId=${encodeURIComponent(scopeId)}&scopeKind=${encodeURIComponent(scopeKind)}`,
      { credentials: 'include' },
    );
    if (!res.ok) return null;
    const data = (await res.json()) as DevJobToken;
    if (data?.token) {
      cachedDevToken = data;
      return data;
    }
    return null;
  } catch {
    return null;
  }
}

export async function enqueueBackgroundJob(params: {
  jobType: 'ExamGeneration' | 'CsvImport' | 'SampleJob';
  payloadReference: string;
  idempotencyKey: string;
  scopeKind?: 'School' | 'MinistryExam';
  scopeResourceId?: string;
}): Promise<{ job: JobApiItem; isLiveApi: boolean }> {
  const scopeKind = params.scopeKind ?? 'School';
  const scopeResourceId = params.scopeResourceId ?? 'school-hn-01';
  const devToken = await acquireDevJobToken(scopeResourceId, scopeKind);

  if (devToken) {
    try {
      const res = await fetch('/api/v1/jobs', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${devToken.token}`,
          'X-Correlation-Id': `ui-${params.idempotencyKey}`,
        },
        body: JSON.stringify({
          jobType: params.jobType,
          payloadReference: params.payloadReference,
          scopeKind: devToken.scopeKind,
          scopeResourceId: devToken.scopeId,
          idempotencyKey: params.idempotencyKey,
        }),
      });
      if (res.ok || res.status === 202) {
        const data = (await res.json()) as { jobId: string; status: JobLifecycleStatus };
        const created: JobApiItem = {
          jobId: data.jobId,
          jobType: params.jobType,
          status: data.status ?? 'Pending',
          progressPercentage: 0,
          resultId: null,
          errorCode: null,
          safeMessage: null,
          isRetryEligible: null,
          actionableReason: null,
          createdAt: new Date().toISOString(),
          payloadReference: params.payloadReference,
          isSimulated: false,
        };
        upsertTrackedJob(created);
        return { job: created, isLiveApi: true };
      }
    } catch {
      // Fall through to labeled simulation
    }
  }

  // Labeled local fallback when backend job service is not running
  const simId = `sim-job-${Date.now().toString(36)}`;
  const isErrorSim = params.payloadReference.includes('simulate-');
  const simulated: JobApiItem = {
    jobId: simId,
    jobType: params.jobType,
    status: 'Running',
    progressPercentage: 25,
    resultId: null,
    errorCode: isErrorSim ? 'BEDROCK_THROTTLED' : null,
    safeMessage: isErrorSim
      ? 'Dịch vụ AI tạm thời quá tải (HTTP 429 ThrottlingException).'
      : null,
    isRetryEligible: isErrorSim ? true : null,
    actionableReason: isErrorSim
      ? 'Đợi vài giây rồi bấm Thử lại để tái sử dụng khóa chống trùng lặp (Idempotency).'
      : null,
    createdAt: new Date().toISOString(),
    payloadReference: params.payloadReference,
    isSimulated: true,
  };
  upsertTrackedJob(simulated);
  return { job: simulated, isLiveApi: false };
}

export async function fetchJobStatus(jobId: string): Promise<JobApiItem | null> {
  if (jobId.startsWith('sim-job-')) {
    const tracked = loadTrackedJobs().find(j => j.jobId === jobId);
    if (!tracked) return null;
    if (isTerminalJobStatus(tracked.status)) return tracked;

    const nextProgress = Math.min(100, (tracked.progressPercentage ?? 0) + 35);
    const shouldFail =
      tracked.payloadReference?.includes('simulate-') && !tracked.payloadReference?.includes('retried');
    const updated: JobApiItem = shouldFail && nextProgress >= 60
      ? {
          ...tracked,
          status: 'Failed',
          progressPercentage: 60,
          errorCode: tracked.errorCode ?? 'BEDROCK_MODEL_TIMEOUT',
          safeMessage: tracked.safeMessage ?? 'Tác vụ vượt quá thời gian chờ phản hồi từ mô hình AI.',
          isRetryEligible: true,
          actionableReason:
            tracked.actionableReason ?? 'Kiểm tra kết nối hoặc bấm Thử lại để chạy lượt thực thi mới.',
        }
      : {
          ...tracked,
          status: nextProgress >= 100 ? 'Completed' : 'Running',
          progressPercentage: nextProgress,
          resultId: nextProgress >= 100 ? `snap-${jobId.slice(-6)}` : null,
          errorCode: null,
          safeMessage: nextProgress >= 100 ? 'Hoàn tất xử lý an toàn.' : 'Đang thực thi tác vụ nền…',
        };
    upsertTrackedJob(updated);
    return updated;
  }

  const devToken = await acquireDevJobToken();
  if (!devToken) return null;
  try {
    const res = await fetch(`/api/v1/jobs/${encodeURIComponent(jobId)}`, {
      headers: { Authorization: `Bearer ${devToken.token}` },
    });
    if (!res.ok) return null;
    const data = (await res.json()) as JobApiItem;
    const merged: JobApiItem = { ...data, isSimulated: false };
    upsertTrackedJob(merged);
    return merged;
  } catch {
    return null;
  }
}

export async function fetchScopedJobs(statusFilter?: string): Promise<{ jobs: JobApiItem[]; isLiveApi: boolean }> {
  const devToken = await acquireDevJobToken();
  if (devToken) {
    try {
      const qs = new URLSearchParams({
        scopeKind: devToken.scopeKind,
        scopeResourceId: devToken.scopeId,
      });
      if (statusFilter) qs.set('status', statusFilter);
      const res = await fetch(`/api/v1/jobs?${qs.toString()}`, {
        headers: { Authorization: `Bearer ${devToken.token}` },
      });
      if (res.ok) {
        const list = (await res.json()) as JobApiItem[];
        const normalized = list.map(j => ({ ...j, isSimulated: false }));
        normalized.forEach(upsertTrackedJob);
        return { jobs: normalized, isLiveApi: true };
      }
    } catch {
      // Fall through to local tracked jobs
    }
  }
  const local = loadTrackedJobs();
  const filtered = statusFilter ? local.filter(j => j.status === statusFilter) : local;
  return { jobs: filtered, isLiveApi: false };
}

export async function retryBackgroundJob(jobId: string, reason?: string): Promise<JobApiItem | null> {
  if (jobId.startsWith('sim-job-')) {
    const tracked = loadTrackedJobs().find(j => j.jobId === jobId);
    if (!tracked) return null;
    const retried: JobApiItem = {
      ...tracked,
      status: 'Completed',
      progressPercentage: 100,
      resultId: `recovered-${jobId.slice(-6)}`,
      errorCode: null,
      safeMessage: 'Đã khôi phục và hoàn tất ở lượt thử lại (Check-before-write).',
      isRetryEligible: false,
      actionableReason: null,
      payloadReference: `${tracked.payloadReference ?? ''}:retried`,
    };
    upsertTrackedJob(retried);
    return retried;
  }

  const devToken = await acquireDevJobToken();
  if (!devToken) return null;
  try {
    const res = await fetch(`/api/v1/jobs/${encodeURIComponent(jobId)}/retry`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${devToken.token}`,
      },
      body: JSON.stringify({ reason: reason ?? 'Người dùng yêu cầu thử lại từ giao diện' }),
    });
    if (!res.ok) return null;
    const updated = (await res.json()) as JobApiItem;
    upsertTrackedJob({ ...updated, isSimulated: false });
    return updated;
  } catch {
    return null;
  }
}

export async function cancelBackgroundJob(jobId: string): Promise<boolean> {
  if (jobId.startsWith('sim-job-')) {
    const tracked = loadTrackedJobs().find(j => j.jobId === jobId);
    if (!tracked) return false;
    upsertTrackedJob({
      ...tracked,
      status: 'Cancelled',
      safeMessage: 'Đã hủy tác vụ theo yêu cầu người dùng.',
    });
    return true;
  }

  const devToken = await acquireDevJobToken();
  if (!devToken) return false;
  try {
    const res = await fetch(`/api/v1/jobs/${encodeURIComponent(jobId)}/cancel`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${devToken.token}` },
    });
    return res.ok;
  } catch {
    return false;
  }
}

export async function fetchJobAttempts(jobId: string): Promise<JobAttemptItem[]> {
  if (jobId.startsWith('sim-job-')) {
    const tracked = loadTrackedJobs().find(j => j.jobId === jobId);
    if (!tracked) return [];
    return [
      {
        id: `att-${jobId}-1`,
        jobId,
        attemptNumber: 1,
        status: tracked.status,
        workerId: 'local-sim-worker-01',
        startedAt: tracked.createdAt ?? new Date().toISOString(),
        finishedAt: isTerminalJobStatus(tracked.status) ? new Date().toISOString() : null,
        executionDurationMs: 420,
        errorCode: tracked.errorCode,
        safeErrorMessage: tracked.safeMessage,
        retryReason: null,
        isManualRetry: false,
      },
    ];
  }

  const devToken = await acquireDevJobToken();
  if (!devToken) return [];
  try {
    const res = await fetch(`/api/v1/jobs/${encodeURIComponent(jobId)}/attempts`, {
      headers: { Authorization: `Bearer ${devToken.token}` },
    });
    if (!res.ok) return [];
    return (await res.json()) as JobAttemptItem[];
  } catch {
    return [];
  }
}

/* ============================================================
   M3-04 AI DRAFT SAFETY GATE (Client Contract Mirror)
   Mirrors Backend/Modules/Review/Domain/AiDraftSafetyGate.cs
   PolicyVersion: m3-ai-draft-v1
   ============================================================ */

export const AI_SAFETY_POLICY_VERSION = 'm3-ai-draft-v1';

export interface AiDraftGateCheck {
  succeeded: boolean;
  policyVersion: string;
  action: 'REVIEW_REQUIRED' | 'BLOCK';
  error: string | null;
}

export function validateAiDraftSafety(input: {
  stem: string;
  options: string[];
  correctIndex: number;
  explanation?: string;
  rawStatus?: string;
  rawReviewerId?: string;
}): AiDraftGateCheck {
  if (input.rawStatus && input.rawStatus !== 'DRAFT') {
    return {
      succeeded: false,
      policyVersion: AI_SAFETY_POLICY_VERSION,
      action: 'BLOCK',
      error: 'Phản hồi AI chứa trường trạng thái không được phép (cấm tự gán APPROVED).',
    };
  }
  if (input.rawReviewerId) {
    return {
      succeeded: false,
      policyVersion: AI_SAFETY_POLICY_VERSION,
      action: 'BLOCK',
      error: 'Phản hồi AI chứa reviewerId không hợp lệ.',
    };
  }
  const stem = input.stem?.trim() ?? '';
  if (!stem || stem.length > 4000) {
    return {
      succeeded: false,
      policyVersion: AI_SAFETY_POLICY_VERSION,
      action: 'BLOCK',
      error: 'Nội dung câu hỏi (stem) trống hoặc vượt quá 4.000 ký tự.',
    };
  }
  if (!Array.isArray(input.options) || input.options.length !== 4) {
    return {
      succeeded: false,
      policyVersion: AI_SAFETY_POLICY_VERSION,
      action: 'BLOCK',
      error: 'Phải có đúng 4 phương án A, B, C, D.',
    };
  }
  const trimmed = input.options.map(o => o.trim());
  if (trimmed.some(o => !o || o.length > 1000)) {
    return {
      succeeded: false,
      policyVersion: AI_SAFETY_POLICY_VERSION,
      action: 'BLOCK',
      error: 'Có phương án trống hoặc vượt quá 1.000 ký tự.',
    };
  }
  if (new Set(trimmed.map(o => o.toLowerCase())).size !== 4) {
    return {
      succeeded: false,
      policyVersion: AI_SAFETY_POLICY_VERSION,
      action: 'BLOCK',
      error: '4 phương án A, B, C, D phải khác biệt nhau.',
    };
  }
  if (!Number.isInteger(input.correctIndex) || input.correctIndex < 0 || input.correctIndex > 3) {
    return {
      succeeded: false,
      policyVersion: AI_SAFETY_POLICY_VERSION,
      action: 'BLOCK',
      error: 'Đáp án đúng phải thuộc một trong 4 phương án A, B, C, D.',
    };
  }
  return {
    succeeded: true,
    policyVersion: AI_SAFETY_POLICY_VERSION,
    action: 'REVIEW_REQUIRED',
    error: null,
  };
}
