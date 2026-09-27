using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class PurchaseOrderPayment
    {
        public int Id { get; set; }

        [Required]
        public int PurchaseOrderId { get; set; }
        public virtual PurchaseOrder PurchaseOrder { get; set; }

        public DateTime PaymentDate { get; set; }

        [Required]
        [StringLength(50)]
        public string Method { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [StringLength(200)]
        public string? ReferenceNumber { get; set; }
    }
}
