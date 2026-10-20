using AiExamBank.Modules.Identity.Contracts;

namespace AiExamBank.Modules.Identity.Tests
{
    [TestClass]
    public sealed class RoleAssignmentTests
    {
        [TestMethod]
        public void Teacher_requires_school_and_department()
        {
            var userId = Guid.NewGuid();
            var schoolId = Guid.NewGuid();
            var departmentId = Guid.NewGuid();

            var assignment = RoleAssignment.Create(
                userId, AppRole.Teacher, schoolId, departmentId);

            Assert.AreEqual(schoolId, assignment.SchoolId);
            Assert.ThrowsExactly<ArgumentException>(() =>
                RoleAssignment.Create(userId, AppRole.Teacher, schoolId, null));
        }

        [TestMethod]
        public void SchoolAdmin_cannot_be_assigned_to_a_department()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                RoleAssignment.Create(
                    Guid.NewGuid(),
                    AppRole.SchoolAdmin,
                    Guid.NewGuid(),
                    Guid.NewGuid()));
        }
    }
}
