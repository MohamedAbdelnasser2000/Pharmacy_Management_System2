using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class ECommerceOrderItem
    {
        public int Id { get; set; }

        [Required]
        public int ECommerceOrderId { get; set; }
        public virtual ECommerceOrder ECommerceOrder { get; set; }

        [Required]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        [Display(Name = "اسم المنتج")]
        [StringLength(200)]
        public string ProductName { get; set; }

        [Display(Name = "الكمية")]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "سعر الوحدة")]
        public decimal UnitPrice { get; set; }

        [NotMapped]
        [Display(Name = "الإجمالي")]
        public decimal LineTotal => Quantity * UnitPrice;
    }
}
