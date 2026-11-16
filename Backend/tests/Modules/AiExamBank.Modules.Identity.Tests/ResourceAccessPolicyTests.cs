using AiExamBank.Modules.Identity.Authorization;
using AiExamBank.Modules.Identity.Contracts;

namespace AiExamBank.Modules.Identity.Tests;

[TestClass]
public sealed class ResourceAccessPolicyTests
{
    [TestMethod]
    public void Department_head_reviews_only_submitted_questions_from_others()
    //Ba khẳng định lần lượt kiểm: được duyệt câu đã gửi của người khác, không tự duyệt, và không duyệt câu nháp
    {
        // // Tạo ID giả cho người duyệt, người viết, trường và bộ môn.
        var reviewer = Guid.NewGuid();
        var author = Guid.NewGuid();
        var school = Guid.NewGuid();
        var department = Guid.NewGuid();

        // Gán vai trò Trưởng bộ môn cho reviewer tại đúng trường và bộ môn.
        var assignment = RoleAssignment.Create(
            reviewer, AppRole.DepartmentHead, school, department);
        // true nghĩa là câu của người khác đã được gửi duyệt: phải cho phép.
        var submitted = new SchoolResource(
            ResourceKind.Question, school, department, author, true);
        Assert.IsTrue(ResourceAccessPolicy.CanAccess(
            reviewer, submitted, ResourceAction.Review, [assignment]));
        // Reviewer cũng là người viết: phải từ chối tự duyệt.
        var ownQuestion = new SchoolResource(
            ResourceKind.Question, school, department, reviewer, true);
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(
            reviewer, ownQuestion, ResourceAction.Review, [assignment]));
        // false nghĩa là câu chưa gửi duyệt: phải từ chối.
        var draft = new SchoolResource(
            ResourceKind.Question, school, department, author, false);
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(
            reviewer, draft, ResourceAction.Review, [assignment]));
    }
    [TestMethod]
    public void Teacher_can_edit_only_owned_resource_in_assigned_department()
    {
        // Tạo ID giả cho giáo viên, trường và bộ môn.
        var teacher = Guid.NewGuid();
        var school = Guid.NewGuid();
        var department = Guid.NewGuid();

        // Giáo viên được phân công vào đúng trường và bộ môn này.
        var assignment = RoleAssignment.Create(teacher, AppRole.Teacher, school, department);

        // Được sửa câu hỏi do chính mình tạo trong bộ môn được giao.
        Assert.IsTrue(ResourceAccessPolicy.CanAccess(teacher,
            new SchoolResource(ResourceKind.Question, school, department, teacher),
            ResourceAction.Edit, [assignment]));

        // Không được sửa câu hỏi của người khác.
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(teacher,
            new SchoolResource(ResourceKind.Question, school, department, Guid.NewGuid()),
            ResourceAction.Edit, [assignment]));

        // Không được sửa câu hỏi thuộc trường khác.
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(teacher,
            new SchoolResource(ResourceKind.Question, Guid.NewGuid(), department, teacher),
            ResourceAction.Edit, [assignment]));

        // Không được sửa ma trận đề thuộc bộ môn khác.
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(teacher,
            new SchoolResource(ResourceKind.Blueprint, school, Guid.NewGuid(), teacher),
            ResourceAction.Edit, [assignment]));
    }

    [TestMethod]
    public void Department_head_can_review_only_same_department_and_not_own_work()
    {
        // Tạo một Trưởng bộ môn với quyền tại một trường và bộ môn cụ thể.
        var head = Guid.NewGuid();
        var school = Guid.NewGuid();
        var department = Guid.NewGuid();
        var assignment = RoleAssignment.Create(head, AppRole.DepartmentHead, school, department);

        // Được xem xét đề đã gửi, thuộc đúng phạm vi, do người khác tạo.
        Assert.IsTrue(ResourceAccessPolicy.CanAccess(head,
            new SchoolResource(ResourceKind.Blueprint, school, department, Guid.NewGuid(), true),
            ResourceAction.Review, [assignment]));

        // Không được tự xem xét đề do chính mình tạo.
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(head,
            new SchoolResource(ResourceKind.Blueprint, school, department, head, true),
            ResourceAction.Review, [assignment]));

        // Không được xem xét câu hỏi thuộc bộ môn khác.
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(head,
            new SchoolResource(ResourceKind.Question, school, Guid.NewGuid(), Guid.NewGuid(), true),
            ResourceAction.Review, [assignment]));

        // Không được xem xét câu hỏi thuộc trường khác.
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(head,
            new SchoolResource(ResourceKind.Question, Guid.NewGuid(), department, Guid.NewGuid(), true),
            ResourceAction.Review, [assignment]));

        // Không được đọc câu hỏi chưa được gửi duyệt.
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(head,
            new SchoolResource(ResourceKind.Question, school, department, Guid.NewGuid()),
            ResourceAction.Read, [assignment]));
    }

    [TestMethod]
    public void School_admin_and_ministry_admin_have_no_implicit_exam_content_access()
    {
        // Tạo một câu hỏi thuộc một trường và bộ môn.
        var actor = Guid.NewGuid();
        var school = Guid.NewGuid();
        var department = Guid.NewGuid();
        var resource = new SchoolResource(ResourceKind.Question, school, department, Guid.NewGuid());

        // SchoolAdmin không tự động có quyền đọc nội dung câu hỏi.
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(actor, resource, ResourceAction.Read,
            [RoleAssignment.Create(actor, AppRole.SchoolAdmin, school, null)]));

        // MinistryAdmin cũng không tự động có quyền đọc nội dung câu hỏi cấp trường.
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(actor, resource, ResourceAction.Read,
            [RoleAssignment.Create(actor, AppRole.MinistryAdmin, null, null)]));
    }

    [TestMethod]
    public void Job_status_requires_original_requester_and_current_school_role()
    {
        // Tạo một job do requester yêu cầu trong một trường.
        var requester = Guid.NewGuid();
        var school = Guid.NewGuid();
        var job = new SchoolResource(ResourceKind.Job, school, null, requester);
        var assignment = RoleAssignment.Create(requester, AppRole.Teacher, school, Guid.NewGuid());

        // Người tạo job còn quyền tại trường được xem trạng thái.
        Assert.IsTrue(ResourceAccessPolicy.CanAccess(requester, job, ResourceAction.Read, [assignment]));

        // Người khác không được xem job này.
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(Guid.NewGuid(), job, ResourceAction.Read, [assignment]));

        // Người tạo đã mất quyền tại trường cũng không được xem.
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(requester, job, ResourceAction.Read, []));

        // Quyền xem trạng thái không đồng nghĩa với quyền sửa job.
        Assert.IsFalse(ResourceAccessPolicy.CanAccess(requester, job, ResourceAction.Edit, [assignment]));
    }

}
