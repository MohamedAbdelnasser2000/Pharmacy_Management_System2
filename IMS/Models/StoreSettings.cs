using System.ComponentModel.DataAnnotations;

namespace IMS.Models
{
    public class StoreSettings
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المخزن مطلوب")]
        [StringLength(200)]
        [Display(Name = "اسم المخزن")]
        public string StoreName { get; set; } = "نظام إدارة المخازن";

        [StringLength(100)]
        [Display(Name = "رقم التليفون")]
        public string? PhoneNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "رقم تليفون إضافي")]
        public string? PhoneNumber2 { get; set; }

        [StringLength(300)]
        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        [StringLength(100)]
        [Display(Name = "البريد الإلكتروني")]
        public string? Email { get; set; }

        [StringLength(100)]
        [Display(Name = "الموقع الإلكتروني")]
        public string? Website { get; set; }

        [StringLength(100)]
        [Display(Name = "الرقم الضريبي")]
        public string? TaxNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "السجل التجاري")]
        public string? CommercialRegister { get; set; }

        [Display(Name = "شعار المخزن (URL)")]
        [StringLength(500)]
        public string? LogoUrl { get; set; }
    }
}
