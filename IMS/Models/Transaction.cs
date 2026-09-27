
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class Transaction
    {
        public int Id { get; set; }

        public DateTime TransactionDate { get; set; }

        [Required]
        [StringLength(50)]
        public string TransactionType { get; set; } // e.g., Sale, Purchase, Payment

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        public int? OrderId { get; set; }
        public virtual Order? Order { get; set; }

        public int? PurchaseOrderId { get; set; }
        public virtual PurchaseOrder? PurchaseOrder { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }
    }
}
