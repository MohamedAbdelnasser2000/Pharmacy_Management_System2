using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class ECommerceOrder
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "رقم الطلب مطلوب")]
        [StringLength(20)]
        [Display(Name = "رقم الطلب")]
        public string OrderNumber { get; set; }

        [Required(ErrorMessage = "اسم العميل مطلوب")]
        [StringLength(100)]
        [Display(Name = "اسم العميل")]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        [StringLength(100)]
        [Display(Name = "البريد الإلكتروني")]
        public string CustomerEmail { get; set; }

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [StringLength(20)]
        [Display(Name = "رقم الهاتف")]
        public string CustomerPhone { get; set; }

        [Required(ErrorMessage = "العنوان مطلوب")]
        [StringLength(500)]
        [Display(Name = "عنوان الشحن")]
        public string ShippingAddress { get; set; }

        [StringLength(100)]
        [Display(Name = "المدينة")]
        public string? City { get; set; }

        [StringLength(100)]
        [Display(Name = "المنطقة")]
        public string? Region { get; set; }

        [StringLength(20)]
        [Display(Name = "الرمز البريدي")]
        public string? PostalCode { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "المبلغ الإجمالي")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "رسوم الشحن")]
        public decimal ShippingFee { get; set; } = 0;

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "المبلغ النهائي")]
        public decimal GrandTotal { get; set; }

        [Required]
        [StringLength(30)]
        [Display(Name = "حالة الطلب")]
        public string Status { get; set; } = "Pending";

        [StringLength(30)]
        [Display(Name = "طريقة الدفع")]
        public string? PaymentMethod { get; set; } = "CashOnDelivery";

        [StringLength(1000)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "تاريخ الطلب")]
        public DateTime OrderDate { get; set; } = AppClock.UtcNow;

        [Display(Name = "تاريخ التحديث")]
        public DateTime? UpdatedDate { get; set; }

        public virtual ICollection<ECommerceOrderItem> OrderItems { get; set; } = new HashSet<ECommerceOrderItem>();
    }

    public static class ECommerceOrderStatus
    {
        public const string Pending = "Pending";
        public const string Processing = "Processing";
        public const string Shipped = "Shipped";
        public const string Delivered = "Delivered";
        public const string Cancelled = "Cancelled";

        public static string GetArabicName(string status) => status switch
        {
            Pending => "قيد الانتظار",
            Processing => "قيد التجهيز",
            Shipped => "تم الشحن",
            Delivered => "تم التوصيل",
            Cancelled => "ملغي",
            _ => status
        };

        public static string GetBadgeClass(string status) => status switch
        {
            Pending => "bg-warning",
            Processing => "bg-info",
            Shipped => "bg-primary",
            Delivered => "bg-success",
            Cancelled => "bg-danger",
            _ => "bg-secondary"
        };
    }
}
