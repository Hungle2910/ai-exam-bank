using System;
using System.Collections.Generic;
using System.Text;

namespace AiExamBank.Modules.Identity.Contracts
{
    public sealed record RoleAssignment
    {
        private RoleAssignment(Guid userId, AppRole role, Guid? schoolId, Guid? departmentId)
        {
            UserId = userId;
            Role = role;
            SchoolId = schoolId;
            DepartmentId = departmentId;
        }

        public Guid UserId { get; }
        public AppRole Role { get; }
        public Guid? SchoolId { get; } // ko phải vai trò nào cx gắn với bộ môn
        public Guid? DepartmentId { get; }

        //quản trị Bộ không gắn với trường; quản trị trường phải có trường; giáo viên và trưởng bộ môn phải có cả trường lẫn bộ môn
        public static RoleAssignment Create(
            Guid userId, AppRole role, Guid? schoolId, Guid? departmentId)
        {
            if (userId == Guid.Empty) throw new ArgumentException("User is required.");
            if (schoolId == Guid.Empty || departmentId == Guid.Empty)
                throw new ArgumentException("Scope ID cannot be empty.");

            bool validScope = role switch
            {
                AppRole.MinistryAdmin => schoolId is null && departmentId is null,
                AppRole.SchoolAdmin => schoolId is not null && departmentId is null,
                AppRole.Teacher or AppRole.DepartmentHead =>
                    schoolId is not null && departmentId is not null,
                _ => false
            };

            if (!validScope) throw new ArgumentException("Invalid scope for this role.");
            return new RoleAssignment(userId, role, schoolId, departmentId);
        }
    }
}
