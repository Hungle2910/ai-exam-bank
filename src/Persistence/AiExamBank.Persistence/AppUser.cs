using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.AspNetCore.Identity;

namespace AiExamBank.Persistence
{
    public class AppUser : IdentityUser<Guid>
    {
        public Guid? SchoolId { get; set; }
    }
}
