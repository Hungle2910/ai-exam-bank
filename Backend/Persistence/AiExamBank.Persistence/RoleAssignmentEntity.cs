using System;
using System.Collections.Generic;
using System.Text;

using AiExamBank.Modules.Identity.Contracts;

namespace AiExamBank.Persistence
{
    public sealed class RoleAssignmentEntity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public AppRole Role { get; set; }
        public Guid? SchoolId { get; set; }
        public Guid? DepartmentId { get; set; }
    }
}
