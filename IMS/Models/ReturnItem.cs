using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class ReturnItem
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "رقم المرتجع")]
        public int ReturnId { get; set; }
        public virtual Return Return { get; set; }

        [Required]
        [Display(Name = "المنتج")]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        [Display(Name = "عنصر الطلب الأصلي")]
        public int? OrderItemId { get; set; }
        public virtual OrderItem? OrderItem { get; set; }

        [Required]
        [Display(Name = "الكمية المرتجعة")]
        public int ReturnedQuantity { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "سعر الوحدة")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(5, 2)")]
        [Display(Name = "نسبة الخصم")]
        public decimal DiscountPercentage { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "مبلغ الاسترداد")]
        public decimal RefundAmount { get; set; }

        [StringLength(500)]
        [Display(Name = "سبب إرجاع المنتج")]
        public string? ItemReason { get; set; }

        [StringLength(50)]
        [Display(Name = "حالة المنتج")]
        public string? Condition { get; set; } // جديد، مستخدم، تالف

        [Display(Name = "إعادة للمخزن")]
        public bool RestockItem { get; set; } = true;

        [NotMapped]
        public decimal TotalRefund => RefundAmount;
    }
}
