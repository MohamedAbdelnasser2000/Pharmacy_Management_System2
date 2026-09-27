
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class Order
    {
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; }

        public DateTime OrderDate { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "المبلغ الإجمالي")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "المبلغ المدفوع")]
        public decimal PaidAmount { get; set; }

        [StringLength(50)]
        public string? Status { get; set; } // e.g., Confirmed, Shipped, Completed, Returned

        // Insurance fields
        [Display(Name = "البوليصة التأمينية")]
        public int? InsurancePolicyId { get; set; }
        public virtual InsurancePolicy? InsurancePolicy { get; set; }

        [Display(Name = "الفرع")]
        public int? BranchId { get; set; }
        public virtual Branch? Branch { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "مبلغ التغطية التأمينية")]
        public decimal InsuranceCoverageAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "المبلغ المتبقي على العميل")]
        public decimal CustomerPayableAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "نسبة التغطية المطبقة")]
        public decimal? AppliedCoveragePercentage { get; set; }

        [Display(Name = "كود الخصم")]
        public int? DiscountCodeId { get; set; }
        public virtual DiscountCode? DiscountCode { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "قيمة الخصم الإضافي")]
        public decimal DiscountAmount { get; set; } = 0;

        public virtual ICollection<OrderItem> OrderItems { get; set; } = new HashSet<OrderItem>();
    }
}
