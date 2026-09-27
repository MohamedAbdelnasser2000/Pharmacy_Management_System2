using System.ComponentModel.DataAnnotations;

namespace IMS.Models
{
    public class ExpiryAlert
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "المنتج")]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        [Display(Name = "المستودع")]
        public int? WarehouseId { get; set; }
        public virtual Warehouse? Warehouse { get; set; }

        [Display(Name = "تاريخ الانتهاء")]
        public DateTime ExpiryDate { get; set; }

        [Display(Name = "الكمية")]
        public int Quantity { get; set; }

        [StringLength(50)]
        [Display(Name = "حالة التنبيه")]
        public string AlertStatus { get; set; } // منتهي، قريب من الانتهاء، تحذير

        [Display(Name = "تاريخ التنبيه")]
        public DateTime AlertDate { get; set; } = AppClock.UtcNow;

        [Display(Name = "تم المعالجة")]
        public bool IsHandled { get; set; } = false;

        [Display(Name = "تاريخ المعالجة")]
        public DateTime? HandledDate { get; set; }

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [StringLength(100)]
        [Display(Name = "الإجراء المتخذ")]
        public string? ActionTaken { get; set; } // تم الإرجاع، تم التخلص، تم البيع بخصم
    }
}
