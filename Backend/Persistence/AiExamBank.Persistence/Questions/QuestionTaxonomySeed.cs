namespace AiExamBank.Persistence.Questions;

/// <summary>
/// Grade-12 THPT taxonomy proposed in docs/M4-01_QUESTION_DOMAIN.md. IDs are fixed so a topic keeps the same
/// ID in every environment. Topics still need checking against the source documents before this is merged.
/// </summary>
public static class QuestionTaxonomySeed
{
    public static readonly Guid MathId = Guid.Parse("8ab2a612-3558-435f-9e60-ef3f8e24a8a8");
    public static readonly Guid PhysicsId = Guid.Parse("1d9f0a75-b153-4626-a9c4-6d4830641c6a");
    public static readonly Guid ChemistryId = Guid.Parse("4d779815-9f88-4ba2-a3e4-0b6916a4407b");

    public static IReadOnlyList<SubjectEntity> Subjects { get; } =
    [
        new() { Id = MathId, Code = "MATH", Name = "Toán" },
        new() { Id = PhysicsId, Code = "PHYS", Name = "Vật lý" },
        new() { Id = ChemistryId, Code = "CHEM", Name = "Hóa học" }
    ];

    public static IReadOnlyList<TopicEntity> Topics { get; } =
    [
        new() { Id = Guid.Parse("223f2355-059b-4195-80e0-6056ea6115f6"), SubjectId = MathId, Code = "MATH.DERIV_APP", Name = "Ứng dụng đạo hàm để khảo sát hàm số" },
        new() { Id = Guid.Parse("699edf31-9caf-43ae-8e38-603bcdf3ba4d"), SubjectId = MathId, Code = "MATH.INTEGRAL", Name = "Nguyên hàm và tích phân" },
        new() { Id = Guid.Parse("fbd23806-f00a-48d2-abc0-84790d72452a"), SubjectId = MathId, Code = "MATH.OXYZ", Name = "Phương pháp tọa độ trong không gian" },
        new() { Id = Guid.Parse("7e1d4f7a-bf13-4c8a-9db5-8f34f51f1fd9"), SubjectId = PhysicsId, Code = "PHYS.IDEAL_GAS", Name = "Khí lí tưởng" },
        new() { Id = Guid.Parse("6d4e7dd8-e7ce-4f69-808a-c8f4bd22b514"), SubjectId = PhysicsId, Code = "PHYS.MAGNETIC", Name = "Từ trường" },
        new() { Id = Guid.Parse("7ecd1b83-2cb8-4026-9e8e-89d2dd0fb171"), SubjectId = PhysicsId, Code = "PHYS.NUCLEAR", Name = "Vật lí hạt nhân và phóng xạ" },
        new() { Id = Guid.Parse("f5da0e27-7b7e-4a06-951f-049c29f0596f"), SubjectId = ChemistryId, Code = "CHEM.ESTER_LIPID", Name = "Ester – Lipid" },
        new() { Id = Guid.Parse("24b94d5e-208b-471a-9b1f-f8ee87241cdd"), SubjectId = ChemistryId, Code = "CHEM.CARBOHYDRATE", Name = "Carbohydrate" },
        new() { Id = Guid.Parse("e8d1686c-50ad-4b59-866d-2eb558286919"), SubjectId = ChemistryId, Code = "CHEM.ELECTROCHEM", Name = "Pin điện và điện phân" }
    ];
}
