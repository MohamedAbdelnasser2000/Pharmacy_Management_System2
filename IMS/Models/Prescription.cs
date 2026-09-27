using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class Prescription
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "رقم الوصفة")]
        public string PrescriptionNumber { get; set; }

        [Display(Name = "تاريخ الوصفة")]
        public DateTime PrescriptionDate { get; set; } = AppClock.UtcNow;

        [Display(Name = "الطبيب")]
        public int? DoctorId { get; set; }
        public virtual Doctor? Doctor { get; set; }

        [Display(Name = "العميل")]
        public int? CustomerId { get; set; }
        public virtual Customer? Customer { get; set; }

        [StringLength(200)]
        [Display(Name = "اسم المريض")]
        public string? PatientName { get; set; }

        [Display(Name = "عمر المريض")]
        public int? PatientAge { get; set; }

        [StringLength(500)]
        [Display(Name = "التشخيص")]
        public string? Diagnosis { get; set; }

        [StringLength(1000)]
        [Display(Name = "ملاحظات الطبيب")]
        public string? DoctorNotes { get; set; }

        [Display(Name = "تاريخ الصرف")]
        public DateTime? DispensedDate { get; set; }

        [Display(Name = "تم الصرف")]
        public bool IsDispensed { get; set; } = false;

        [Display(Name = "رقم الطلب المرتبط")]
        public int? OrderId { get; set; }
        public virtual Order? Order { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "إجمالي المبلغ")]
        public decimal? TotalAmount { get; set; }

        [StringLength(500)]
        [Display(Name = "ملاحظات الصيدلي")]
        public string? PharmacistNotes { get; set; }

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedDate { get; set; } = AppClock.UtcNow;

        // Insurance fields
        [Display(Name = "البوليصة التأمينية")]
        public int? InsurancePolicyId { get; set; }
        public virtual InsurancePolicy? InsurancePolicy { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "مبلغ التغطية التأمينية")]
        public decimal InsuranceCoverageAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "المبلغ المتبقي على العميل")]
        public decimal CustomerPayableAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "نسبة التغطية المطبقة")]
        public decimal? AppliedCoveragePercentage { get; set; }

        // Navigation properties
        public virtual ICollection<PrescriptionItem> PrescriptionItems { get; set; } = new HashSet<PrescriptionItem>();
    }
}
