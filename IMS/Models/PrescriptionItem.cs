using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class PrescriptionItem
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "الوصفة")]
        public int PrescriptionId { get; set; }
        public virtual Prescription Prescription { get; set; }

        [Required]
        [Display(Name = "المنتج")]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        [Required]
        [Display(Name = "الكمية")]
        public int Quantity { get; set; }

        [StringLength(200)]
        [Display(Name = "الجرعة")]
        public string? Dosage { get; set; }

        [StringLength(200)]
        [Display(Name = "التكرار")]
        public string? Frequency { get; set; } // مرة يومياً، مرتين يومياً، إلخ

        [Display(Name = "المدة (بالأيام)")]
        public int? Duration { get; set; }

        [StringLength(500)]
        [Display(Name = "تعليمات الاستخدام")]
        public string? Instructions { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "السعر")]
        public decimal? Price { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "الإجمالي")]
        public decimal? Total { get; set; }
    }
}
