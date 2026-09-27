using System.ComponentModel.DataAnnotations;

namespace IMS.Models
{
    public class Doctor
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الطبيب مطلوب")]
        [StringLength(200)]
        [Display(Name = "اسم الطبيب")]
        public string Name { get; set; }

        [StringLength(100)]
        [Display(Name = "التخصص")]
        public string? Specialization { get; set; }

        [StringLength(50)]
        [Display(Name = "رقم الترخيص")]
        public string? LicenseNumber { get; set; }

        [StringLength(20)]
        [Display(Name = "رقم الهاتف")]
        public string? Phone { get; set; }

        [StringLength(100)]
        [Display(Name = "البريد الإلكتروني")]
        [EmailAddress]
        public string? Email { get; set; }

        [StringLength(300)]
        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        [StringLength(200)]
        [Display(Name = "المستشفى/العيادة")]
        public string? Hospital { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "تاريخ الإضافة")]
        public DateTime CreatedDate { get; set; } = AppClock.UtcNow;

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        // Navigation properties
        public virtual ICollection<Prescription> Prescriptions { get; set; } = new HashSet<Prescription>();
    }
}
