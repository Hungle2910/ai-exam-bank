using AiExamBank.Modules.Exams.Domain;

namespace AiExamBank.Modules.Exams.Tests;

[TestClass]
public sealed class BlueprintMatrixTests
{
    private static BlueprintMatrix Valid() => new("Ma trận", Guid.NewGuid(), 45, 3, 0.75m,
        [new(Guid.NewGuid(), "EASY", 3, 0.25m)]);

    [TestMethod]
    public void Exact_decimal_totals_are_accepted() => Assert.IsEmpty(Valid().Validate());

    [TestMethod]
    public void Declared_totals_must_match_slots()
    {
        var errors = (Valid() with { TotalQuestions = 4, TotalPoints = 1 }).Validate();
        Assert.IsTrue(errors.ContainsKey("totalQuestions"));
        Assert.IsTrue(errors.ContainsKey("totalPoints"));
    }

    [TestMethod]
    public void Duplicate_cells_and_unknown_difficulty_are_rejected()
    {
        var value = Valid();
        Assert.IsTrue((value with { Slots = [value.Slots[0], value.Slots[0]] }).Validate().ContainsKey("slots[1]"));
        Assert.IsTrue((value with { Slots = [value.Slots[0] with { Difficulty = "UNKNOWN" }] }).Validate().ContainsKey("slots[0].difficulty"));
    }

    [TestMethod]
    public void Extreme_points_return_validation_instead_of_overflow()
    {
        var value = Valid();
        Assert.IsNotEmpty((value with { Slots = [value.Slots[0] with { PointsPerQuestion = decimal.MaxValue }] }).Validate());
    }
}
