
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IMS.Models
{
    public class Supplier
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الاسم مطلوب")]
        [StringLength(100)]
        [Display(Name = "اسم المورد")]
        public string Name { get; set; }

        [StringLength(200)]
        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        [StringLength(20)]
        [Display(Name = "رقم الهاتف")]
        public string? PhoneNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "البريد الإلكتروني")]
        public string? Email { get; set; }

        [StringLength(50)]
        [Display(Name = "الحساب البنكي")]
        public string? BankAccount { get; set; }

        public virtual ICollection<Product> Products { get; set; } = new HashSet<Product>();
        public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new HashSet<PurchaseOrder>();
        public virtual SupplierAccount Account { get; set; }
        public virtual ICollection<SupplierLedgerEntry> LedgerEntries { get; set; } = new HashSet<SupplierLedgerEntry>();
    }
}
