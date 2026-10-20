using AiExamBank.Modules.Identity.Authorization;
using AiExamBank.Modules.Identity.Contracts;

namespace AiExamBank.Modules.Identity.Tests;

[TestClass]
public sealed class ResourceAccessPolicyTests
{
    [TestMethod]
    public void Teacher_can_edit_only_owned_resource_in_assigned_department()
    {
        var teacher = Guid.NewGuid();
        var school = Guid.NewGuid();
        var department = Guid.NewGuid();
        var assignment = RoleAssignment.Create(teacher, AppRole.Teacher, school, department);

        Assert.IsTrue(ResourceAccessPolicy.CanAccess(teacher,
            new SchoolResource(ResourceKind.Question, school, department, teacher),
            ResourceAction.Edit, [assignment]));
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(teacher,
            new SchoolResource(ResourceKind.Question, school, department, Guid.NewGuid()),
            ResourceAction.Edit, [assignment]));
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(teacher,
            new SchoolResource(ResourceKind.Question, Guid.NewGuid(), department, teacher),
            ResourceAction.Edit, [assignment]));
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(teacher,
            new SchoolResource(ResourceKind.Blueprint, school, Guid.NewGuid(), teacher),
            ResourceAction.Edit, [assignment]));
    }

    [TestMethod]
    public void Department_head_can_review_only_same_department_and_not_own_work()
    {
        var head = Guid.NewGuid();
        var school = Guid.NewGuid();
        var department = Guid.NewGuid();
        var assignment = RoleAssignment.Create(head, AppRole.DepartmentHead, school, department);

        Assert.IsTrue(ResourceAccessPolicy.CanAccess(head,
            new SchoolResource(ResourceKind.Blueprint, school, department, Guid.NewGuid(), true),
            ResourceAction.Review, [assignment]));
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(head,
            new SchoolResource(ResourceKind.Blueprint, school, department, head, true),
            ResourceAction.Review, [assignment]));
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(head,
            new SchoolResource(ResourceKind.Question, school, Guid.NewGuid(), Guid.NewGuid(), true),
            ResourceAction.Review, [assignment]));
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(head,
            new SchoolResource(ResourceKind.Question, Guid.NewGuid(), department, Guid.NewGuid(), true),
            ResourceAction.Review, [assignment]));
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(head,
            new SchoolResource(ResourceKind.Question, school, department, Guid.NewGuid()),
            ResourceAction.Read, [assignment]));
    }

    [TestMethod]
    public void School_admin_and_ministry_admin_have_no_implicit_exam_content_access()
    {
        var actor = Guid.NewGuid();
        var school = Guid.NewGuid();
        var department = Guid.NewGuid();
        var resource = new SchoolResource(ResourceKind.Question, school, department, Guid.NewGuid());

        Assert.IsFalse(ResourceAccessPolicy.CanAccess(actor, resource, ResourceAction.Read,
            [RoleAssignment.Create(actor, AppRole.SchoolAdmin, school, null)]));
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(actor, resource, ResourceAction.Read,
            [RoleAssignment.Create(actor, AppRole.MinistryAdmin, null, null)]));
    }

    [TestMethod]
    public void Job_status_requires_original_requester_and_current_school_role()
    {
        var requester = Guid.NewGuid();
        var school = Guid.NewGuid();
        var job = new SchoolResource(ResourceKind.Job, school, null, requester);
        var assignment = RoleAssignment.Create(requester, AppRole.Teacher, school, Guid.NewGuid());

        Assert.IsTrue(ResourceAccessPolicy.CanAccess(requester, job, ResourceAction.Read, [assignment]));
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(Guid.NewGuid(), job, ResourceAction.Read, [assignment]));
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(requester, job, ResourceAction.Read, []));
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(requester, job, ResourceAction.Edit, [assignment]));
    }
}
