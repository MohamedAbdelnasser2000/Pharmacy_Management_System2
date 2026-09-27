using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IMS.Models
{
    public class Branch
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(20)]
        public string Code { get; set; }

        [StringLength(200)]
        public string? Location { get; set; }

        public virtual ICollection<Warehouse> Warehouses { get; set; } = new HashSet<Warehouse>();
    }
}
