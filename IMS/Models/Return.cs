using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class Return
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "رقم الطلب مطلوب")]
        [Display(Name = "رقم الطلب")]
        public int OrderId { get; set; }
        public virtual Order Order { get; set; }

        [Display(Name = "العميل")]
        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; }

        [Required]
        [Display(Name = "تاريخ المرتجع")]
        public DateTime ReturnDate { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "حالة المرتجع")]
        public string Status { get; set; } // معلق، مقبول، مرفوض، مكتمل

        [Required]
        [StringLength(20)]
        [Display(Name = "نوع المرتجع")]
        public string ReturnType { get; set; } // استرجاع كامل، استرجاع جزئي

        [Required]
        [StringLength(500)]
        [Display(Name = "سبب المرتجع")]
        public string Reason { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "إجمالي المبلغ المسترجع")]
        public decimal TotalRefundAmount { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "المبلغ المسترد")]
        public decimal RefundedAmount { get; set; }

        [StringLength(20)]
        [Display(Name = "طريقة الاسترداد")]
        public string? RefundMethod { get; set; } // نقدي، تحويل بنكي، رصيد في الحساب

        [Display(Name = "تاريخ الاسترداد")]
        public DateTime? RefundDate { get; set; }

        [StringLength(1000)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "تم بواسطة")]
        public string? ProcessedBy { get; set; }

        [Display(Name = "تاريخ الموافقة")]
        public DateTime? ApprovedDate { get; set; }

        [Display(Name = "تمت الموافقة بواسطة")]
        public string? ApprovedBy { get; set; }

        public virtual ICollection<ReturnItem> ReturnItems { get; set; } = new HashSet<ReturnItem>();
    }
}
