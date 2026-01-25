using Microsoft.AspNetCore.Identity;
using System;

namespace Cherry.Core.Entities
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string DisplayName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ApplicationRole : IdentityRole<Guid>
    {
        public ApplicationRole() { }
        public ApplicationRole(string roleName) : base(roleName) { }
    }
}
