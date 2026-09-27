using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class SupplierAccount
    {
        public int Id { get; set; }

        [Required]
        public int SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal OpeningBalance { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal CurrentBalance { get; set; }

        public virtual ICollection<SupplierLedgerEntry> LedgerEntries { get; set; } = new HashSet<SupplierLedgerEntry>();
    }
}
