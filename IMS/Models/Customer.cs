
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IMS.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الاسم مطلوب")]
        [StringLength(100)]
        [Display(Name = "اسم العميل")]
        public string Name { get; set; }

        [StringLength(50)]
        [Display(Name = "رقم الهوية / جواز السفر")]
        public string? NationalId { get; set; }

        [Display(Name = "تاريخ الميلاد")]
        public DateTime? DateOfBirth { get; set; }

        [StringLength(10)]
        [Display(Name = "الجنس")]
        public string? Gender { get; set; } // ذكر، أنثى

        [StringLength(10)]
        [Display(Name = "فصيلة الدم")]
        public string? BloodType { get; set; } // A+, A-, B+, B-, O+, O-, AB+, AB-

        [StringLength(200)]
        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        [StringLength(20)]
        [Display(Name = "رقم الهاتف")]
        public string? PhoneNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "البريد الإلكتروني")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        public string? Email { get; set; }

        // الحقول الطبية الحرجة
        [StringLength(1000)]
        [Display(Name = "الحساسية الدوائية")]
        public string? Allergies { get; set; }

        [StringLength(1000)]
        [Display(Name = "الأمراض المزمنة")]
        public string? ChronicDiseases { get; set; }

        [StringLength(500)]
        [Display(Name = "ملاحظات طبية")]
        public string? MedicalNotes { get; set; }

        [StringLength(100)]
        [Display(Name = "جهة اتصال الطوارئ")]
        public string? EmergencyContactName { get; set; }

        [StringLength(20)]
        [Display(Name = "رقم هاتف الطوارئ")]
        public string? EmergencyContactPhone { get; set; }

        [Display(Name = "تاريخ التسجيل")]
        public DateTime CreatedDate { get; set; } = AppClock.UtcNow;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        public virtual ICollection<Order> Orders { get; set; } = new HashSet<Order>();
    }
}
