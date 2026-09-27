using System.ComponentModel.DataAnnotations;

namespace IMS.ViewModels
{
    public class ProductViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "اسم المنتج")]
        public string Name { get; set; }

        [Display(Name = "الكود")]
        public string Code { get; set; }

        [Display(Name = "الوصف")]
        public string Description { get; set; }

        [Display(Name = "السعر")]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [Display(Name = "الكمية")]
        public int Quantity { get; set; }

        [Display(Name = "حد التنبيه")]
        public int LowStockThreshold { get; set; }

        [Display(Name = "الموقع")]
        public string Location { get; set; }

        [Display(Name = "المورد")]
        public int? SupplierId { get; set; }
    }
}