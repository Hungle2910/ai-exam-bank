using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.AspNetCore.Identity;

namespace AiExamBank.Persistence
{
    public class AppUser : IdentityUser<Guid> // cung cấp ID dạng Guid, email và thông tin đăng nhập
    {
        public Guid? SchoolId { get; set; }
    }
}
