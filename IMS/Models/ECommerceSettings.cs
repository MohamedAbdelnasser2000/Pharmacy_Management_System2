using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class ECommerceSettings
    {
        [Key]
        public int Id { get; set; }

        [StringLength(200)]
        [Display(Name = "عنوان البانر الرئيسي")]
        public string BannerTitle { get; set; } = "صيدليتك أونلاين";

        [StringLength(500)]
        [Display(Name = "وصف البانر")]
        public string BannerSubtitle { get; set; } = "احصل على أدويتك ومستلزماتك الطبية بسهولة من منزلك";

        [Display(Name = "المتجر نشط")]
        public bool IsStoreActive { get; set; } = true;

        [Display(Name = "يتطلب تسجيل دخول للشراء")]
        public bool RequireLoginToPurchase { get; set; } = false;

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "حد الشحن المجاني")]
        public decimal FreeShippingThreshold { get; set; } = 200;

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "رسوم الشحن الافتراضية")]
        public decimal DefaultShippingFee { get; set; } = 30;

        [StringLength(2000)]
        [Display(Name = "شروط الخدمة")]
        public string? TermsOfService { get; set; }

        [StringLength(500)]
        [Display(Name = "رسالة أعلى المتجر")]
        public string? AnnouncementMessage { get; set; }

        [Display(Name = "إظهار المنتجات التي تحتاج وصفة")]
        public bool ShowPrescriptionProducts { get; set; } = false;
    }
}
