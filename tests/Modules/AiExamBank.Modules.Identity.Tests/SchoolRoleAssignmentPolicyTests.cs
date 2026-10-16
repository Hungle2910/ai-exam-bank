using AiExamBank.Modules.Identity.Authorization;
using AiExamBank.Modules.Identity.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiExamBank.Modules.Identity.Tests
{
    [TestClass]
    public sealed class SchoolRoleAssignmentPolicyTests
    {
        [TestMethod]
        public void SchoolAdmin_can_assign_teacher_in_own_school()
        {
            var adminId = Guid.NewGuid();
            var schoolA = Guid.NewGuid();
            var assignments = new[]
            {
            RoleAssignment.Create(adminId, AppRole.SchoolAdmin, schoolA, null)
        };

            Assert.IsTrue(SchoolRoleAssignmentPolicy.CanAssign(
                adminId, schoolA, AppRole.Teacher, assignments));
        }

        [TestMethod]
        public void SchoolAdmin_cannot_assign_outside_school_or_grant_admin_role()
        {
            var adminId = Guid.NewGuid();
            var schoolA = Guid.NewGuid();
            var schoolB = Guid.NewGuid();
            var assignments = new[]
            {
            RoleAssignment.Create(adminId, AppRole.SchoolAdmin, schoolA, null)
        };

            Assert.IsFalse(SchoolRoleAssignmentPolicy.CanAssign(
                adminId, schoolB, AppRole.Teacher, assignments));
            Assert.IsFalse(SchoolRoleAssignmentPolicy.CanAssign(
                adminId, schoolA, AppRole.SchoolAdmin, assignments));
        }
    }
}
