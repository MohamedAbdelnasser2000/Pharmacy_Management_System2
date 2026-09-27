using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class ProductInventory
    {
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        [Required]
        public int WarehouseId { get; set; }
        public virtual Warehouse Warehouse { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal OnHandQuantity { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal ReservedQuantity { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal ReorderPoint { get; set; }

        public DateTime LastUpdated { get; set; }

        public DateTime? NextExpiryDate { get; set; }

        [NotMapped]
        public decimal AvailableQuantity => OnHandQuantity - ReservedQuantity;
    }
}
