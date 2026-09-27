using Microsoft.AspNetCore.Identity;

namespace IMS.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }

        public string? BranchName { get; set; }

        public int? BranchId { get; set; }
        public virtual Branch? Branch { get; set; }

        public bool IsActive { get; set; } = true;

        public string? ProfilePicture { get; set; }

        public DateTime CreatedDate { get; set; } = AppClock.UtcNow;

        public DateTime? LastLoginDate { get; set; }
    }
}