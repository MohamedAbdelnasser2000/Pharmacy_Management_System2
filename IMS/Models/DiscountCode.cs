using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class DiscountCode
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "كود الخصم")]
        public string Code { get; set; }

        [Column(TypeName = "decimal(5, 2)")]
        [Display(Name = "نسبة الخصم (%)")]
        public decimal? Percentage { get; set; }

        [Display(Name = "قيمة الخصم الثابتة")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? FixedAmount { get; set; }

        [Display(Name = "تاريخ البدء")]
        public DateTime StartDate { get; set; }

        [Display(Name = "تاريخ الانتهاء")]
        public DateTime EndDate { get; set; }

        [Display(Name = "الحد الأدنى للطلب")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal MinimumOrderAmount { get; set; } = 0;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "عدد مرات الاستخدام المسموح بها")]
        public int UsageLimit { get; set; } = 1000;

        [Display(Name = "عدد مرات الاستخدام الحالية")]
        public int CurrentUsage { get; set; } = 0;
    }
}
