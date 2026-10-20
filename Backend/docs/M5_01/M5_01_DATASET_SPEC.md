# M5-01: ML Dataset Spec (Khung dữ liệu cho AI)

## Mục đích
Đây là **đề xuất cho nhánh ML tùy chọn**, không phải yêu cầu triển khai M5-01 hoặc dịch vụ đang chạy. Chỉ sử dụng câu hỏi/đáp án khi chủ dữ liệu cho phép, người dùng có quyền với phạm vi trường/kỳ thi, và bộ dữ liệu được lưu mã hóa với quyền truy cập/retention xác định. Không đưa nội dung đề thi mật hoặc dữ liệu học sinh vào dataset mặc định.

## Cấu trúc Rubric (bản nháp JSON Schema)

Dữ liệu để AI đánh giá độ khó sẽ bao gồm nội dung câu hỏi, các lựa chọn, đáp án đúng, và các tiêu chí sư phạm (Bloom's Taxonomy).

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "QuestionDifficultyRubric",
  "type": "object",
  "properties": {
    "QuestionId": {
      "type": "string",
      "description": "Mã định danh duy nhất của câu hỏi."
    },
    "Subject": {
      "type": "string",
      "description": "Môn học (Ví dụ: Toán, Lý, Hóa)."
    },
    "GradeLevel": {
      "type": "integer",
      "description": "Lớp học (Ví dụ: 10, 11, 12)."
    },
    "ContentText": {
      "type": "string",
      "description": "Nội dung văn bản của câu hỏi."
    },
    "Choices": {
      "type": "array",
      "minItems": 2,
      "uniqueItems": true,
      "items": {
        "type": "string"
      },
      "description": "Danh sách các lựa chọn (đối với trắc nghiệm)."
    },
    "CorrectAnswer": {
      "type": "string",
      "description": "Đáp án đúng."
    },
    "BloomsTaxonomyLevel": {
      "type": "string",
      "enum": ["Remember", "Understand", "Apply", "Analyze", "Evaluate", "Create"],
      "description": "Mức độ nhận thức theo thang đo Bloom."
    },
    "ExpectedDifficulty": {
      "type": "string",
      "enum": ["Easy", "Medium", "Hard", "VeryHard"],
      "description": "Độ khó dự kiến (do giáo viên gán)."
    },
    "EstimatedSolvingTimeSeconds": {
      "type": "integer",
      "description": "Thời gian giải bài dự kiến (giây)."
    }
  },
  "required": ["QuestionId", "ContentText", "Choices", "CorrectAnswer", "BloomsTaxonomyLevel"]
}
```

## Giải thích sử dụng
1. MVP chỉ có MCQ một đáp án: `Choices` và `CorrectAnswer` luôn bắt buộc. Validator ứng dụng còn phải kiểm tra đáp án đúng là một lựa chọn duy nhất trong `Choices`; JSON Schema này không tự kiểm tra quan hệ giữa hai trường.
2. Chỉ tạo dataset khi nhánh ML được duyệt, có quyền sử dụng nội dung và đánh giá chi phí/bảo mật.
3. Giữ nhãn do giáo viên gán là nhãn gốc; kết quả AI là advisory, không tự ghi đè câu đã duyệt.
4. Dữ liệu kết quả học sinh nếu được xem xét sau này cần một quyết định privacy và consent riêng, không thuộc schema này.
