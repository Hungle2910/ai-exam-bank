# M5-01: ML Dataset Spec (Khung dữ liệu cho AI)

## Mục đích
Chuẩn bị cấu trúc Rubric (Dataset Schema) để phục vụ cho việc huấn luyện hoặc đánh giá độ khó của câu hỏi bởi AI (Amazon SageMaker / Bedrock) vào Tuần 7.

## Cấu trúc Rubric (JSON Schema)

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
  "required": ["QuestionId", "ContentText", "BloomsTaxonomyLevel"]
}
```

## Giải thích sử dụng
1. Khi có một lượng lớn câu hỏi được import, hệ thống sẽ trích xuất dữ liệu theo định dạng JSON trên.
2. Dataset này có thể được sử dụng làm input prompt cho LLM để nhờ LLM gán nhãn lại độ khó hoặc so sánh độ khó giữa các câu.
3. Trong tương lai, tập dữ liệu này nếu có thêm trường `ActualStudentPerformance` (% học sinh làm đúng) sẽ được dùng để fine-tune ML models.
