using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class InsurancePolicy
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "رقم البوليصة")]
        public string PolicyNumber { get; set; }

        [Required]
        [Display(Name = "شركة التأمين")]
        public int InsuranceId { get; set; }
        public virtual Insurance Insurance { get; set; }

        [Display(Name = "العميل")]
        public int? CustomerId { get; set; }
        public virtual Customer? Customer { get; set; }

        [StringLength(200)]
        [Display(Name = "اسم حامل البوليصة")]
        public string? PolicyHolderName { get; set; }

        [Display(Name = "تاريخ البداية")]
        public DateTime StartDate { get; set; }

        [Display(Name = "تاريخ الانتهاء")]
        public DateTime EndDate { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "نسبة التغطية (%)")]
        public decimal CoveragePercentage { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "الحد الأقصى للتغطية")]
        public decimal? MaxCoverageAmount { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "المبلغ المستخدم")]
        public decimal UsedAmount { get; set; } = 0;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedDate { get; set; } = AppClock.UtcNow;

        [NotMapped]
        [Display(Name = "المبلغ المتبقي")]
        public decimal RemainingAmount => (MaxCoverageAmount ?? 0) - UsedAmount;

        [NotMapped]
        [Display(Name = "صالح")]
        public bool IsValid => IsActive && AppClock.UtcNow >= StartDate && AppClock.UtcNow <= EndDate;
    }
}
