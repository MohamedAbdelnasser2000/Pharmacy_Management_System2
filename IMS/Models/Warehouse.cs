using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IMS.Models
{
    public class Warehouse
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المستودع مطلوب")]
        [StringLength(100)]
        [Display(Name = "اسم المستودع")]
        public string Name { get; set; }

        [Required(ErrorMessage = "الفرع مطلوب")]
        [Display(Name = "الفرع")]
        public int BranchId { get; set; }
        public virtual Branch Branch { get; set; }

        [StringLength(200)]
        [Display(Name = "الموقع")]
        public string? Location { get; set; }

        public virtual ICollection<Product> Products { get; set; } = new HashSet<Product>();
        public virtual ICollection<ProductInventory> ProductInventories { get; set; } = new HashSet<ProductInventory>();
    }
}
