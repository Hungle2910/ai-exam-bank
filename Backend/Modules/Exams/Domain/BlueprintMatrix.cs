namespace AiExamBank.Modules.Exams.Domain;

/// <summary>Validated matrix values; taxonomy membership is checked by the catalog adapter.</summary>
public sealed record MatrixSlot(Guid TopicId, string Difficulty, int QuestionCount, decimal PointsPerQuestion);

public sealed record BlueprintMatrix(string Name, Guid SubjectId, int DurationMinutes,
    int TotalQuestions, decimal TotalPoints, IReadOnlyList<MatrixSlot> Slots)
{
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string key, string message) => errors[key] = [message];
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 200) Add("name", "Tên phải có từ 1 đến 200 ký tự.");
        if (SubjectId == Guid.Empty) Add("subjectId", "Chọn môn học.");
        if (DurationMinutes is < 1 or > 1440) Add("durationMinutes", "Thời gian phải từ 1 đến 1440 phút.");
        if (TotalQuestions is < 1 or > 1000) Add("totalQuestions", "Tổng số câu phải từ 1 đến 1000.");
        if (TotalPoints <= 0 || TotalPoints > 10000 || decimal.Round(TotalPoints, 4) != TotalPoints)
            Add("totalPoints", "Tổng điểm phải lớn hơn 0, tối đa 10000 và tối đa 4 chữ số thập phân.");
        if (Slots is null || Slots.Count is < 1 or > 200)
        {
            Add("slots", "Ma trận cần từ 1 đến 200 ô.");
            return errors;
        }
        var keys = new HashSet<(Guid, string)>();
        long count = 0;
        decimal points = 0;
        for (var i = 0; i < Slots.Count; i++)
        {
            var slot = Slots[i];
            if (slot is null) { Add($"slots[{i}]", "Ô không hợp lệ."); continue; }
            if (slot.TopicId == Guid.Empty) Add($"slots[{i}].topicId", "Chọn chủ đề.");
            if (slot.Difficulty is not ("EASY" or "MEDIUM" or "HARD")) Add($"slots[{i}].difficulty", "Độ khó không hợp lệ.");
            if (!keys.Add((slot.TopicId, slot.Difficulty))) Add($"slots[{i}]", "Chủ đề và độ khó bị trùng.");
            if (slot.QuestionCount is < 1 or > 1000) Add($"slots[{i}].questionCount", "Số câu phải từ 1 đến 1000.");
            if (slot.PointsPerQuestion <= 0 || slot.PointsPerQuestion > 10000 || decimal.Round(slot.PointsPerQuestion, 4) != slot.PointsPerQuestion)
                Add($"slots[{i}].pointsPerQuestion", "Điểm mỗi câu không hợp lệ (tối đa 4 chữ số thập phân).");
            else if (slot.QuestionCount is >= 1 and <= 1000) points += slot.QuestionCount * slot.PointsPerQuestion;
            count += slot.QuestionCount;
        }
        if (count != TotalQuestions) Add("totalQuestions", "Tổng số câu phải bằng tổng các ô.");
        if (points != TotalPoints) Add("totalPoints", "Tổng điểm phải bằng tổng các ô.");
        return errors;
    }
}
