-- =============================================================================
-- Migration: 001_create_jobs_and_job_attempts.sql
-- Module: M5 Jobs (Reliability & Durable Execution Engine)
-- Database Engine: PostgreSQL 16+ (tương thích EF Core Npgsql)
-- =============================================================================

-- 1. Bảng jobs: Lưu trữ thông tin định danh, phạm vi phân quyền, khóa Idempotency và trạng thái vòng đời của tác vụ
CREATE TABLE IF NOT EXISTS jobs (
    id UUID PRIMARY KEY,
    type VARCHAR(100) NOT NULL,
    status VARCHAR(20) NOT NULL,
    payload_reference TEXT NOT NULL,
    result_id TEXT NULL,
    created_by VARCHAR(100) NOT NULL,
    scope_kind VARCHAR(50) NOT NULL,
    scope_resource_id VARCHAR(100) NOT NULL,
    idempotency_key VARCHAR(256) NOT NULL,
    request_fingerprint VARCHAR(256) NOT NULL,
    correlation_id VARCHAR(256) NOT NULL,
    available_at TIMESTAMPTZ NULL,
    cancellation_requested_at TIMESTAMPTZ NULL,
    attempt_count INT NOT NULL DEFAULT 0,
    progress_percentage INT NULL,
    error_code VARCHAR(100) NULL,
    safe_error_message TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,

    -- Ràng buộc kiểm tra tính hợp lệ
    CONSTRAINT chk_jobs_status CHECK (status IN ('Pending', 'Running', 'Completed', 'Failed', 'Cancelled')),
    CONSTRAINT chk_jobs_progress CHECK (progress_percentage IS NULL OR (progress_percentage >= 0 AND progress_percentage <= 100)),
    CONSTRAINT chk_jobs_attempt_count CHECK (attempt_count >= 0 AND attempt_count <= 3)
);

-- Khóa chống trùng lặp nghiệp vụ (Idempotency Uniqueness Index):
-- Chuẩn hóa UPPER(scope_resource_id) đồng nhất với FileJobStore và InMemoryJobStore
CREATE UNIQUE INDEX IF NOT EXISTS uq_jobs_idempotency_upper
ON jobs (scope_kind, UPPER(scope_resource_id), created_by, type, idempotency_key);

-- Chỉ mục tối ưu hóa cho Worker Polling tìm job Pending hoặc đến hạn chạy
CREATE INDEX IF NOT EXISTS idx_jobs_polling ON jobs (status, available_at)
WHERE status = 'Pending';

-- Chỉ mục phục vụ tra cứu theo phạm vi phân quyền cho Status UI và API (hỗ trợ so sánh scope_resource_id không phân biệt hoa thường)
CREATE INDEX IF NOT EXISTS idx_jobs_scope_status ON jobs (scope_kind, UPPER(scope_resource_id), status, created_at DESC);

-- Chỉ mục liên kết vết giám sát Correlation ID
CREATE INDEX IF NOT EXISTS idx_jobs_correlation ON jobs (correlation_id);


-- 2. Bảng job_attempts: Ghi lại từng lượt chạy (Attempt 1..3) của Worker kèm LeaseToken độc quyền
CREATE TABLE IF NOT EXISTS job_attempts (
    id UUID PRIMARY KEY,
    job_id UUID NOT NULL REFERENCES jobs(id) ON DELETE CASCADE,
    attempt_number INT NOT NULL,
    status VARCHAR(20) NOT NULL,
    worker_id VARCHAR(100) NOT NULL,
    lease_token UUID NOT NULL,
    started_at TIMESTAMPTZ NOT NULL,
    heartbeat_at TIMESTAMPTZ NOT NULL,
    lease_expires_at TIMESTAMPTZ NOT NULL,
    finished_at TIMESTAMPTZ NULL,
    error_code VARCHAR(100) NULL,
    safe_error_message TEXT NULL,

    -- Ràng buộc kiểm tra
    CONSTRAINT chk_job_attempts_status CHECK (status IN ('Started', 'Completed', 'Failed', 'Abandoned')),
    CONSTRAINT chk_job_attempts_seq CHECK (attempt_number >= 1 AND attempt_number <= 3),

    -- Đảm bảo mỗi lượt chạy của một job là duy nhất
    CONSTRAINT uq_job_attempts_job_seq UNIQUE (job_id, attempt_number)
);

-- Chỉ mục tối ưu cho việc quét thu hồi các Job bị bỏ hoang (Abandoned Leases)
CREATE INDEX IF NOT EXISTS idx_job_attempts_lease_expiry ON job_attempts (status, lease_expires_at)
WHERE status = 'Started';

-- Chỉ mục tìm kiếm lịch sử attempt theo Job
CREATE INDEX IF NOT EXISTS idx_job_attempts_job_id ON job_attempts (job_id, started_at DESC);
