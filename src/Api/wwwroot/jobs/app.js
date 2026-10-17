/**
 * AI Exam Bank — Status UI Dashboard JavaScript
 * Quản lý Polling thích ứng (Adaptive Polling), Bộ lọc trạng thái,
 * Thao tác Hủy (Cancel), Xem chi tiết lỗi an toàn và Khởi tạo Job mới.
 */

document.addEventListener('DOMContentLoaded', () => {
    // State
    let jobsList = [];
    let currentFilter = 'All';
    let searchQuery = '';
    let pollTimer = null;
    let pollIntervalMs = 2000; // 2s mặc định khi có job chạy
    let isPollingActive = true;

    // DOM Elements
    const scopeKindSelect = document.getElementById('scopeKindSelect');
    const scopeIdInput = document.getElementById('scopeIdInput');
    const tokenInput = document.getElementById('tokenInput');
    const btnRefresh = document.getElementById('btnRefresh');
    const liveIndicator = document.getElementById('liveIndicator');
    
    // Stats
    const statTotal = document.getElementById('statTotal');
    const statRunning = document.getElementById('statRunning');
    const statPending = document.getElementById('statPending');
    const statCompleted = document.getElementById('statCompleted');
    const statFailed = document.getElementById('statFailed');

    // Table & Filter
    const filterTabs = document.getElementById('filterTabs');
    const searchInput = document.getElementById('searchInput');
    const tableBody = document.getElementById('jobsTableBody');

    // Modal
    const createModal = document.getElementById('createJobModal');
    const btnOpenCreateModal = document.getElementById('btnCreateJobModal');
    const btnCloseCreateModal = document.getElementById('btnCloseCreateModal');
    const btnCancelCreateModal = document.getElementById('btnCancelCreateModal');
    const createForm = document.getElementById('createJobForm');
    const modalJobType = document.getElementById('modalJobType');
    const modalPayloadRef = document.getElementById('modalPayloadRef');
    const modalIdempotencyKey = document.getElementById('modalIdempotencyKey');
    const btnGenKey = document.getElementById('btnGenKey');

    // Drawer
    const detailDrawer = document.getElementById('detailDrawer');
    const btnCloseDrawer = document.getElementById('btnCloseDrawer');
    const drawerTitle = document.getElementById('drawerTitle');
    const drawerBody = document.getElementById('drawerBody');

    // Toasts
    const toastContainer = document.getElementById('toastContainer');

    // -------------------------------------------------------------
    // Helper Functions
    // -------------------------------------------------------------
    function generateIdempotencyKey() {
        return 'idem-' + Math.random().toString(36).substring(2, 9) + '-' + Date.now().toString(36);
    }

    function showToast(message, type = 'info') {
        const toast = document.createElement('div');
        toast.className = 'toast';
        
        let icon = 'ℹ️';
        if (type === 'success') icon = '✅';
        if (type === 'error') icon = '⚠️';
        
        toast.innerHTML = `<span>${icon}</span> <span>${escapeHtml(message)}</span>`;
        toastContainer.appendChild(toast);

        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transform = 'translateY(10px)';
            toast.style.transition = 'all 0.3s ease';
            setTimeout(() => toast.remove(), 300);
        }, 3500);
    }

    function escapeHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    async function getAuthorizedToken(scopeId) {
        if (tokenInput && tokenInput.value.trim()) {
            return tokenInput.value.trim();
        }

        // Trong môi trường Development, lấy token từ server runtime endpoint
        try {
            const resp = await fetch(`/api/v1/jobs/dev-token?scopeId=${encodeURIComponent(scopeId || '')}`);
            if (resp.ok) {
                const data = await resp.json();
                if (data && data.token) {
                    if (tokenInput) {
                        tokenInput.value = data.token;
                    }
                    return data.token;
                }
            }
        } catch {
            // Endpoint dev-token chỉ khả dụng trong môi trường Development
        }
        return '';
    }

    // -------------------------------------------------------------
    // API Fetching & Adaptive Polling
    // -------------------------------------------------------------
    async function fetchJobs() {
        const scopeKind = scopeKindSelect.value;
        const scopeId = scopeIdInput.value.trim() || 'school-hn-01';

        try {
            const token = await getAuthorizedToken(scopeId);
            const headers = {
                'Accept': 'application/json',
                'X-Scope-Id': scopeId
            };
            if (token) {
                headers['Authorization'] = `Bearer ${token}`;
            }

            const url = `/api/v1/jobs?scopeKind=${encodeURIComponent(scopeKind)}&scopeResourceId=${encodeURIComponent(scopeId)}`;
            const response = await fetch(url, { headers });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: Lỗi tải danh sách tác vụ.`);
            }

            const data = await response.json();
            jobsList = Array.isArray(data) ? data : [];
            updateStats();
            renderTable();

            // Adaptive Polling: nếu còn job Running hoặc Pending thì poll nhanh (2s), nếu không thì giãn cách (5s)
            const hasActiveJobs = jobsList.some(j => j.status === 'Running' || j.status === 'Pending');
            adjustPollingInterval(hasActiveJobs ? 2000 : 5000);
        } catch (err) {
            console.error('Fetch jobs error:', err);
            // Giãn cách polling khi gặp lỗi mạng
            adjustPollingInterval(8000);
        }
    }

    function adjustPollingInterval(newInterval) {
        if (pollIntervalMs === newInterval && pollTimer !== null) return;
        pollIntervalMs = newInterval;
        
        const label = liveIndicator.querySelector('.label');
        if (label) {
            label.textContent = `Auto-Polling (${(pollIntervalMs / 1000).toFixed(0)}s)`;
        }

        if (pollTimer) clearInterval(pollTimer);
        pollTimer = setInterval(fetchJobs, pollIntervalMs);
    }

    // Cleanup khi tab bị ẩn để tiết kiệm tài nguyên
    document.addEventListener('visibilitychange', () => {
        if (document.hidden) {
            if (pollTimer) {
                clearInterval(pollTimer);
                pollTimer = null;
            }
            liveIndicator.querySelector('.dot').classList.remove('pulse');
        } else {
            liveIndicator.querySelector('.dot').classList.add('pulse');
            fetchJobs();
            pollTimer = setInterval(fetchJobs, pollIntervalMs);
        }
    });

    // -------------------------------------------------------------
    // Render Statistics & Filter
    // -------------------------------------------------------------
    function updateStats() {
        const total = jobsList.length;
        const running = jobsList.filter(j => j.status === 'Running').length;
        const pending = jobsList.filter(j => j.status === 'Pending').length;
        const completed = jobsList.filter(j => j.status === 'Completed').length;
        const failed = jobsList.filter(j => j.status === 'Failed').length;

        statTotal.textContent = total;
        statRunning.textContent = running;
        statPending.textContent = pending;
        statCompleted.textContent = completed;
        statFailed.textContent = failed;
    }

    function renderTable() {
        const query = searchQuery.toLowerCase();
        const filtered = jobsList.filter(job => {
            const matchesFilter = currentFilter === 'All' || job.status === currentFilter;
            const matchesSearch = !query || 
                job.jobId.toLowerCase().includes(query) ||
                (job.status && job.status.toLowerCase().includes(query)) ||
                (job.errorCode && job.errorCode.toLowerCase().includes(query)) ||
                (job.resultId && job.resultId.toLowerCase().includes(query));
            return matchesFilter && matchesSearch;
        });

        if (filtered.length === 0) {
            tableBody.innerHTML = `
                <tr>
                    <td colspan="6" class="empty-state">
                        <div class="empty-content">
                            <span class="empty-icon">📂</span>
                            <p>Không tìm thấy tác vụ nào ${currentFilter !== 'All' ? `ở trạng thái ${currentFilter}` : ''}.</p>
                            <button class="btn btn-sm btn-primary" id="btnEmptyCreate">Tạo tác vụ thử nghiệm ngay</button>
                        </div>
                    </td>
                </tr>`;
            const emptyBtn = document.getElementById('btnEmptyCreate');
            if (emptyBtn) emptyBtn.addEventListener('click', () => btnOpenCreateModal.click());
            return;
        }

        tableBody.innerHTML = filtered.map(job => {
            const shortId = job.jobId.substring(0, 8);
            const isRunning = job.status === 'Running';
            const isPending = job.status === 'Pending';
            const isCompleted = job.status === 'Completed';
            const isFailed = job.status === 'Failed';
            const isCancelled = job.status === 'Cancelled';

            // Progress bar
            let progressHtml = '—';
            if (job.progressPercentage !== null && job.progressPercentage !== undefined) {
                progressHtml = `
                    <div class="progress-container">
                        <div class="progress-bar-bg">
                            <div class="progress-bar-fill" style="width: ${job.progressPercentage}%"></div>
                        </div>
                        <span class="progress-text">${job.progressPercentage}%</span>
                    </div>`;
            } else if (isRunning) {
                progressHtml = `
                    <div class="progress-container">
                        <div class="progress-bar-bg">
                            <div class="progress-bar-fill" style="width: 30%"></div>
                        </div>
                        <span class="progress-text">Đang xử lý...</span>
                    </div>`;
            }

            // Result or Safe Error
            let detailHtml = '<span class="text-muted">—</span>';
            if (isCompleted && job.resultId) {
                detailHtml = `<span class="text-emerald" title="${escapeHtml(job.resultId)}">Kết quả: ${escapeHtml(job.resultId)}</span>`;
            } else if (isFailed) {
                detailHtml = `<span class="result-cell has-error" onclick="window.viewJobDetail('${job.jobId}')" title="Bấm để xem chi tiết lỗi">
                    ⚠️ [${escapeHtml(job.errorCode || 'FAILED')}]: ${escapeHtml(job.safeMessage || 'Lỗi không xác định')}
                </span>`;
            } else if (isCancelled) {
                detailHtml = `<span class="text-muted">Tác vụ đã bị hủy</span>`;
            }

            // Actions
            let actionHtml = '';
            if (isPending || isRunning) {
                actionHtml = `<button class="btn btn-xs btn-danger-outline" onclick="window.cancelJob('${job.jobId}')">Hủy</button>`;
            } else if (isCompleted) {
                actionHtml = `<button class="btn btn-xs btn-success-outline" onclick="window.viewJobDetail('${job.jobId}')">Xem KQ</button>`;
            } else if (isFailed) {
                actionHtml = `<button class="btn btn-xs btn-secondary" onclick="window.viewJobDetail('${job.jobId}')">Xem Lỗi</button>`;
            } else {
                actionHtml = `<button class="btn btn-xs btn-secondary" onclick="window.viewJobDetail('${job.jobId}')">Chi tiết</button>`;
            }

            return `
                <tr>
                    <td>
                        <div class="job-id-cell">
                            <span>#${shortId}...</span>
                            <span class="copy-btn" onclick="window.copyToClipboard('${job.jobId}')" title="Copy Job ID">📋</span>
                        </div>
                    </td>
                    <td>
                        <span class="job-type-badge">⚙️ Tác vụ ngầm</span>
                    </td>
                    <td>
                        <span class="status-badge ${job.status}">
                            ${isRunning ? '<span class="spinner-mini"></span>' : ''}
                            ${job.status}
                        </span>
                    </td>
                    <td>${progressHtml}</td>
                    <td><div class="result-cell">${detailHtml}</div></td>
                    <td class="text-right">${actionHtml}</td>
                </tr>`;
        }).join('');
    }

    // -------------------------------------------------------------
    // Action Handlers
    // -------------------------------------------------------------
    window.copyToClipboard = (text) => {
        navigator.clipboard.writeText(text).then(() => {
            showToast('Đã sao chép Job ID vào bộ nhớ đệm.', 'success');
        });
    };

    window.cancelJob = async (jobId) => {
        if (!confirm('Bạn có chắc chắn muốn hủy tác vụ này không?')) return;
        const scopeId = scopeIdInput.value.trim() || 'school-hn-01';

        try {
            const token = await getAuthorizedToken(scopeId);
            const headers = {
                'Accept': 'application/json',
                'X-Scope-Id': scopeId
            };
            if (token) {
                headers['Authorization'] = `Bearer ${token}`;
            }

            const response = await fetch(`/api/v1/jobs/${jobId}/cancel`, {
                method: 'POST',
                headers
            });

            if (response.ok) {
                showToast(`Đã gửi yêu cầu hủy cho Job #${jobId.substring(0, 8)}...`, 'success');
                fetchJobs();
            } else {
                const errData = await response.json();
                showToast(`Không thể hủy: ${errData.detail || 'Lỗi không xác định'}`, 'error');
            }
        } catch (err) {
            showToast('Lỗi kết nối tới máy chủ khi hủy job.', 'error');
        }
    };

    window.viewJobDetail = (jobId) => {
        const job = jobsList.find(j => j.jobId === jobId);
        if (!job) return;

        drawerTitle.textContent = `Tác vụ #${job.jobId.substring(0, 8)}...`;
        
        let errorBannerHtml = '';
        if (job.status === 'Failed') {
            errorBannerHtml = `
                <div class="error-banner">
                    <h4>Mã lỗi: ${escapeHtml(job.errorCode || 'UNKNOWN_ERROR')}</h4>
                    <p><strong>Thông báo an toàn:</strong> ${escapeHtml(job.safeMessage || 'Không có thông điệp cụ thể.')}</p>
                    <p style="margin-top: 0.5rem; font-size: 0.72rem; color: #fda4af;">
                        * Dữ liệu đã được lọc redaction bảo vệ PII và đề thi gốc.
                    </p>
                </div>`;
        }

        drawerBody.innerHTML = `
            <div class="drawer-item">
                <div class="item-label">Job ID Đầy đủ</div>
                <div class="item-value font-mono">${escapeHtml(job.jobId)}</div>
            </div>
            <div class="drawer-item">
                <div class="item-label">Trạng thái hiện tại</div>
                <div class="item-value">
                    <span class="status-badge ${job.status}">${job.status}</span>
                </div>
            </div>
            <div class="drawer-item">
                <div class="item-label">Tiến độ thực tế</div>
                <div class="item-value">${job.progressPercentage !== null ? job.progressPercentage + '%' : 'Không đo lường (null)'}</div>
            </div>
            ${job.resultId ? `
            <div class="drawer-item">
                <div class="item-label">Kết quả sinh ra (ResultId)</div>
                <div class="item-value text-emerald font-mono">${escapeHtml(job.resultId)}</div>
            </div>` : ''}
            ${errorBannerHtml}
        `;

        detailDrawer.classList.add('active');
    };

    // -------------------------------------------------------------
    // Event Listeners
    // -------------------------------------------------------------
    btnRefresh.addEventListener('click', () => {
        showToast('Đang làm mới danh sách...', 'info');
        fetchJobs();
    });

    scopeKindSelect.addEventListener('change', fetchJobs);
    scopeIdInput.addEventListener('change', fetchJobs);
    userIdInput.addEventListener('change', fetchJobs);

    // Tab Filter
    filterTabs.addEventListener('click', (e) => {
        const tab = e.target.closest('.tab');
        if (!tab) return;
        filterTabs.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
        tab.classList.add('active');
        currentFilter = tab.dataset.filter;
        renderTable();
    });

    // Stat card quick filter
    document.querySelectorAll('.stat-card').forEach(card => {
        card.addEventListener('click', () => {
            const status = card.dataset.status;
            const targetTab = filterTabs.querySelector(`.tab[data-filter="${status}"]`);
            if (targetTab) targetTab.click();
        });
    });

    // Search
    searchInput.addEventListener('input', (e) => {
        searchQuery = e.target.value.trim();
        renderTable();
    });

    // Modal
    btnOpenCreateModal.addEventListener('click', () => {
        modalIdempotencyKey.value = generateIdempotencyKey();
        createModal.classList.add('active');
    });

    btnCloseCreateModal.addEventListener('click', () => createModal.classList.remove('active'));
    btnCancelCreateModal.addEventListener('click', () => createModal.classList.remove('active'));
    btnGenKey.addEventListener('click', () => modalIdempotencyKey.value = generateIdempotencyKey());

    // Submit Enqueue Job
    createForm.addEventListener('submit', async (e) => {
        e.preventDefault();
        const submitBtn = document.getElementById('btnSubmitCreateJob');
        submitBtn.disabled = true;
        submitBtn.textContent = 'Đang gửi...';

        const payload = {
            jobType: modalJobType.value,
            payloadReference: modalPayloadRef.value.trim(),
            scopeKind: scopeKindSelect.value,
            scopeResourceId: scopeIdInput.value.trim() || 'school-hn-01',
            idempotencyKey: modalIdempotencyKey.value.trim()
        };

        try {
            const token = await getAuthorizedToken(payload.scopeResourceId);
            const headers = {
                'Content-Type': 'application/json',
                'Accept': 'application/json',
                'X-Scope-Id': payload.scopeResourceId
            };
            if (token) {
                headers['Authorization'] = `Bearer ${token}`;
            }

            const response = await fetch('/api/v1/jobs', {
                method: 'POST',
                headers,
                body: JSON.stringify(payload)
            });

            if (response.status === 202 || response.status === 200 || response.status === 201) {
                const resData = await response.json();
                showToast(`Đã tạo tác vụ thành công! ID: #${resData.jobId.substring(0, 8)}...`, 'success');
                createModal.classList.remove('active');
                fetchJobs();
            } else if (response.status === 409) {
                const errData = await response.json();
                showToast(`Xung đột Idempotency (409): ${errData.detail || 'Khóa trùng nhưng nội dung khác nhau.'}`, 'error');
            } else {
                const errData = await response.json();
                showToast(`Lỗi: ${errData.detail || 'Không thể tạo job'}`, 'error');
            }
        } catch (err) {
            showToast('Lỗi kết nối khi gửi yêu cầu enqueue.', 'error');
        } finally {
            submitBtn.disabled = false;
            submitBtn.textContent = 'Đưa vào hàng đợi (Enqueue)';
        }
    });

    // Drawer close
    btnCloseDrawer.addEventListener('click', () => detailDrawer.classList.remove('active'));
    detailDrawer.addEventListener('click', (e) => {
        if (e.target === detailDrawer) detailDrawer.classList.remove('active');
    });

    // Initial Load
    fetchJobs();
    pollTimer = setInterval(fetchJobs, pollIntervalMs);
});
