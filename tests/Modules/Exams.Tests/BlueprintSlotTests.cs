using AiExamBank.Modules.Exams.Domain;

namespace AiExamBank.Modules.Exams.Tests;

[TestClass]
public sealed class BlueprintSlotTests
{
    [TestMethod]
    public void Create_calculates_total_points_without_losing_decimal_precision()
    {
        var slot = BlueprintSlot.Create(Guid.NewGuid(), 3, 0.25m);

        Assert.AreEqual(0.75m, slot.TotalPoints);
    }

    [TestMethod]
    public void Create_rejects_missing_topic_and_invalid_count_or_points()
    {
        Assert.ThrowsExactly<ArgumentException>(() => BlueprintSlot.Create(Guid.Empty, 1, 1m));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => BlueprintSlot.Create(Guid.NewGuid(), 0, 1m));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => BlueprintSlot.Create(Guid.NewGuid(), 1, 0m));
    }

    [TestMethod]
    public void Create_rejects_a_total_that_cannot_fit_in_decimal()
    {
        Assert.ThrowsExactly<OverflowException>(() => BlueprintSlot.Create(Guid.NewGuid(), 2, decimal.MaxValue));
    }
}
