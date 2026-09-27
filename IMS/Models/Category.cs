using System.ComponentModel.DataAnnotations;

namespace IMS.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الفئة مطلوب")]
        [StringLength(100)]
        [Display(Name = "اسم الفئة")]
        public string Name { get; set; }

        [StringLength(500)]
        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedDate { get; set; } = AppClock.UtcNow;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        // Navigation property
        public virtual ICollection<Product> Products { get; set; } = new HashSet<Product>();
    }
}
