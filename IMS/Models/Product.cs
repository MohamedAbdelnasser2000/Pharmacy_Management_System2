
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace IMS.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        [StringLength(100)]
        [Display(Name = "اسم المنتج")]
        public string Name { get; set; }

        [StringLength(50)]
        [Display(Name = "كود المنتج")]
        public string? Code { get; set; }

        [StringLength(100)]
        [Display(Name = "الباركود")]
        public string? Barcode { get; set; }

        [StringLength(20)]
        [Display(Name = "الوحدة")]
        public string? Unit { get; set; }

        [StringLength(500)]
        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "سعر الشراء")]
        public decimal? Price { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "سعر البيع")]
        public decimal? SalePrice { get; set; }

        [Display(Name = "الحد الأدنى للمخزون")]
        public int? LowStockThreshold { get; set; }

        [Display(Name = "الفئة")]
        public int? CategoryId { get; set; }
        public virtual Category? Category { get; set; }

        [Display(Name = "المورد")]
        public int? SupplierId { get; set; }
        public virtual Supplier? Supplier { get; set; }

        // Pharmacy-specific fields
        [StringLength(200)]
        [Display(Name = "الاسم العلمي")]
        public string? ScientificName { get; set; }

        [StringLength(200)]
        [Display(Name = "الاسم التجاري")]
        public string? TradeName { get; set; }

        [StringLength(200)]
        [Display(Name = "الشركة المصنعة")]
        public string? Manufacturer { get; set; }

        [Display(Name = "تاريخ الإنتاج")]
        public DateTime? ManufacturingDate { get; set; }

        [Display(Name = "تاريخ الانتهاء")]
        public DateTime? ExpiryDate { get; set; }

        [StringLength(100)]
        [Display(Name = "رقم التشغيلة")]
        public string? BatchNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "التركيز")]
        public string? Concentration { get; set; }

        [StringLength(100)]
        [Display(Name = "الشكل الدوائي")]
        public string? DosageForm { get; set; } // أقراص، كبسولات، شراب، حقن، مرهم، قطرة

        [Display(Name = "يتطلب وصفة طبية")]
        public bool RequiresPrescription { get; set; } = false;

        [StringLength(300)]
        [Display(Name = "المادة الفعالة")]
        public string? ActiveIngredient { get; set; }

        [StringLength(200)]
        [Display(Name = "التصنيف الدوائي")]
        public string? DrugClassification { get; set; } // مضاد حيوي، مسكن، خافض حرارة، إلخ

        [StringLength(1000)]
        [Display(Name = "الآثار الجانبية")]
        public string? SideEffects { get; set; }

        [StringLength(1000)]
        [Display(Name = "موانع الاستخدام")]
        public string? Contraindications { get; set; }

        [StringLength(500)]
        [Display(Name = "طريقة الاستخدام")]
        public string? UsageInstructions { get; set; }

        [Display(Name = "درجة حرارة التخزين")]
        public string? StorageTemperature { get; set; }

        [Display(Name = "تحذيرات خاصة")]
        public string? SpecialWarnings { get; set; }

        [Display(Name = "صورة المنتج")]
        public string? ImageUrl { get; set; }
        public bool ShowInEcommerce { get; set; } = true;

        public virtual ICollection<ProductUnit> Units { get; set; } = new HashSet<ProductUnit>();
        public virtual ICollection<ProductInventory> Inventories { get; set; } = new HashSet<ProductInventory>();

        [NotMapped]
        public decimal TotalQuantity => Inventories?.Sum(i => i.OnHandQuantity) ?? 0;

        [NotMapped]
        [Display(Name = "حالة الصلاحية")]
        public string ExpiryStatus
        {
            get
            {
                if (!ExpiryDate.HasValue) return "غير محدد";
                var daysToExpiry = (ExpiryDate.Value - AppClock.UtcNow).Days;
                if (daysToExpiry < 0) return "منتهي";
                if (daysToExpiry <= 30) return "قريب من الانتهاء";
                if (daysToExpiry <= 90) return "تحذير";
                return "صالح";
            }
        }

        [NotMapped]
        public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < AppClock.UtcNow;

        [NotMapped]
        public bool IsNearExpiry => ExpiryDate.HasValue && (ExpiryDate.Value - AppClock.UtcNow).Days <= 90 && (ExpiryDate.Value - AppClock.UtcNow).Days > 0;
    }
}
