using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class StockMovement
    {
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        [Required]
        public int WarehouseId { get; set; }
        public virtual Warehouse Warehouse { get; set; }

        [Required]
        [StringLength(30)]
        public string MovementType { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Quantity { get; set; }

        [Display(Name = "تاريخ الحركة")]
        public DateTime MovementDate { get; set; }

        [Display(Name = "الفرع")]
        public int? BranchId { get; set; }
        public virtual Branch? Branch { get; set; }

        [StringLength(200)]
        public string? Reason { get; set; }

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }
    }
}
