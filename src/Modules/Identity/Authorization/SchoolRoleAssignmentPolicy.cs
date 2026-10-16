using AiExamBank.Modules.Identity.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiExamBank.Modules.Identity.Authorization
{
    public static class SchoolRoleAssignmentPolicy
    {
        //SchoolAdmin gán vai trò Teacher/DepartmentHead trong chính trường của mình
        public static bool CanAssign(
            Guid actorUserId,
            Guid targetUserSchoolId,
            AppRole roleToAssign,
            IReadOnlyCollection<RoleAssignment> currentAssignments)
        {
            ArgumentNullException.ThrowIfNull(currentAssignments);

            if (actorUserId == Guid.Empty || targetUserSchoolId == Guid.Empty)
                return false;

            if (roleToAssign is not (AppRole.Teacher or AppRole.DepartmentHead))
                return false;

            return currentAssignments.Any(assignment =>
                assignment.UserId == actorUserId &&
                assignment.Role == AppRole.SchoolAdmin &&
                assignment.SchoolId == targetUserSchoolId);
        }
    }
}
