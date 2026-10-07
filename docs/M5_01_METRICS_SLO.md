# M5-01: Reliability Metrics Catalog & Initial SLO

## 1. Metric Catalog (Danh mục chỉ số đo lường)
Để theo dõi sức khỏe của hệ thống Background Job, chúng ta cần thu thập và xuất các chỉ số (metrics) sau ra CloudWatch (AWS):

| Metric Name | Type | Description (Ý nghĩa) | Dimensions (Phân loại theo) |
| :--- | :--- | :--- | :--- |
| `JobEnqueuedCount` | Counter | Số lượng Job được đẩy vào hàng đợi (mới khởi tạo). | `JobType` |
| `JobCompletedCount` | Counter | Số lượng Job hoàn thành thành công. | `JobType` |
| `JobFailedCount` | Counter | Số lượng Job bị lỗi (Sau khi đã thử lại hết số lần cho phép). | `JobType`, `ErrorCategory` |
| `JobRetryCount` | Counter | Số lần hệ thống phải tự động thử lại (do AWS chập chờn, hoặc Worker sập). | `JobType` |
| `JobProcessingTime` | Histogram/Timer | Thời gian từ lúc Worker bắt đầu xử lý đến lúc hoàn thành (tính bằng giây). | `JobType` |
| `JobQueueWaitTime` | Histogram/Timer | Thời gian một Job phải nằm chờ trong hàng đợi trước khi có Worker rảnh để gắp ra làm. | `JobType` |
| `ActiveWorkersCount` | Gauge | Số lượng máy chủ (Worker) đang rảnh/đang bận trong hệ thống. | `Status` |

## 2. Initial SLO Đề xuất (Cam kết chất lượng dịch vụ ban đầu)
SLO (Service Level Objective) là những con số mục tiêu mà đội ngũ kỹ thuật cam kết với người dùng/chủ sản phẩm. Nếu vi phạm các chỉ số này, hệ thống sẽ tự động bắn cảnh báo (Alert) cho kỹ sư trực hệ thống.

*   **SLO 1 - Tỷ lệ thành công (Success Rate):**
    *   *Mục tiêu:* **99.5%** các Background Jobs (Import/Generate Exam) phải hoàn thành thành công trong ngày.
    *   *Cảnh báo:* Bắn alert Slack nếu tỷ lệ thành công rớt xuống dưới 95% trong 1 giờ.
*   **SLO 2 - Thời gian chờ (Queue Wait Time):**
    *   *Mục tiêu:* **95%** các yêu cầu (P95) phải được Worker bắt đầu xử lý trong vòng **10 giây** kể từ khi bấm Submit.
    *   *Cảnh báo:* Nếu có hơn 50 Jobs nằm kẹt ở trạng thái `Pending` quá 3 phút (chứng tỏ đang thiếu Worker).
*   **SLO 3 - Thời gian xử lý (Processing Time):**
    *   *Mục tiêu:* Sinh đề thi bằng AI phải xong trong dưới **2 phút** (P90). Import file Excel 1000 dòng phải xong trong dưới **1 phút** (P90).
    *   *Cảnh báo:* Bắn alert nếu quá 30% Job bị kẹt ở trạng thái `Running` lố thời gian timeout.
