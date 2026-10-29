using AiExamBank.Modules.Identity.Authorization;
using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Persistence.Questions;

namespace AiExamBank.Persistence.Tests;

[TestClass]
public sealed class QuestionAccessTests
{
    private static readonly Guid School = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly Guid MathDepartment = Guid.NewGuid();
    private static readonly Guid PhysicsDepartment = Guid.NewGuid();
    private static readonly Guid ChemistryDepartment = Guid.NewGuid();

    private static IReadOnlyList<QuestionRow> AllRows() =>
        (from department in new[] { MathDepartment, PhysicsDepartment, ChemistryDepartment }
         from owner in new[] { Actor, Other }
         from status in Enum.GetValues<QuestionStatus>()
         select new QuestionRow
         {
             QuestionId = Guid.NewGuid(),
             SchoolId = School,
             DepartmentId = department,
             OwnerUserId = owner,
             LatestStatus = status
         }).ToArray();

    private static void AssertListFilterMatchesPolicy(params RoleAssignment[] assignments)
    {
        var predicate = QuestionAccess.VisibleTo(
            Actor,
            QuestionAccess.DepartmentsOf(assignments, AppRole.Teacher),
            QuestionAccess.DepartmentsOf(assignments, AppRole.DepartmentHead)).Compile();

        foreach (var row in AllRows())
        {
            var resource = QuestionAccess.ResourceOf(row.SchoolId, row.DepartmentId, row.OwnerUserId, row.LatestStatus);
            var allowed = ResourceAccessPolicy.CanAccess(Actor, resource, ResourceAction.Read, assignments);

            Assert.AreEqual(allowed, predicate(row),
                $"department {row.DepartmentId}, owner is actor {row.OwnerUserId == Actor}, status {row.LatestStatus}");
        }
    }

    [TestMethod]
    public void Teacher_filter_matches_the_policy()
    {
        AssertListFilterMatchesPolicy(RoleAssignment.Create(Actor, AppRole.Teacher, School, MathDepartment));
    }

    [TestMethod]
    public void Department_head_filter_matches_the_policy()
    {
        AssertListFilterMatchesPolicy(RoleAssignment.Create(Actor, AppRole.DepartmentHead, School, PhysicsDepartment));
    }

    [TestMethod]
    public void Combined_roles_and_several_departments_match_the_policy()
    {
        AssertListFilterMatchesPolicy(
            RoleAssignment.Create(Actor, AppRole.Teacher, School, MathDepartment),
            RoleAssignment.Create(Actor, AppRole.Teacher, School, PhysicsDepartment),
            RoleAssignment.Create(Actor, AppRole.DepartmentHead, School, MathDepartment));
    }

    [TestMethod]
    public void A_school_admin_or_an_unassigned_user_sees_no_questions()
    {
        AssertListFilterMatchesPolicy(RoleAssignment.Create(Actor, AppRole.SchoolAdmin, School, null));
        AssertListFilterMatchesPolicy();
    }

    [TestMethod]
    public void DepartmentsOf_returns_only_the_requested_role_without_duplicates()
    {
        var assignments = new[]
        {
            RoleAssignment.Create(Actor, AppRole.Teacher, School, MathDepartment),
            RoleAssignment.Create(Actor, AppRole.Teacher, School, MathDepartment),
            RoleAssignment.Create(Actor, AppRole.DepartmentHead, School, PhysicsDepartment),
            RoleAssignment.Create(Actor, AppRole.SchoolAdmin, School, null)
        };

        CollectionAssert.AreEqual(new[] { MathDepartment }, QuestionAccess.DepartmentsOf(assignments, AppRole.Teacher).ToArray());
        CollectionAssert.AreEqual(new[] { PhysicsDepartment }, QuestionAccess.DepartmentsOf(assignments, AppRole.DepartmentHead).ToArray());
    }
}
