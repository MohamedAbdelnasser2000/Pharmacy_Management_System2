using System;
using System.ComponentModel.DataAnnotations;

namespace IMS.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "المستخدم")]
        public string UserId { get; set; }
        
        [Display(Name = "اسم المستخدم")]
        public string UserName { get; set; }

        [Required]
        [Display(Name = "العملية")]
        public string Action { get; set; } // Create, Update, Delete, Login, etc.

        [Required]
        [Display(Name = "الجدول/الكيان")]
        public string EntityName { get; set; }

        [Display(Name = "معرف الكيان")]
        public string EntityId { get; set; }

        [Display(Name = "البيانات القديمة")]
        public string? OldValues { get; set; }

        [Display(Name = "البيانات الجديدة")]
        public string? NewValues { get; set; }

        [Display(Name = "تاريخ العملية")]
        public DateTime DateTime { get; set; } = AppClock.UtcNow;

        [Display(Name = "عنوان IP")]
        public string? IpAddress { get; set; }
    }
}
