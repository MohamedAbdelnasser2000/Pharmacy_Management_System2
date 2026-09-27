using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class Insurance
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم شركة التأمين مطلوب")]
        [StringLength(200)]
        [Display(Name = "اسم شركة التأمين")]
        public string Name { get; set; }

        [StringLength(100)]
        [Display(Name = "رمز الشركة")]
        public string? Code { get; set; }

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

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "نسبة التغطية الافتراضية (%)")]
        public decimal DefaultCoveragePercentage { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "أقصى مبلغ تغطية")]
        public decimal MaxCoverageAmount { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "تاريخ الإضافة")]
        public DateTime CreatedDate { get; set; } = AppClock.UtcNow;

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        // Navigation properties
        public virtual ICollection<InsurancePolicy> Policies { get; set; } = new HashSet<InsurancePolicy>();
    }
}
