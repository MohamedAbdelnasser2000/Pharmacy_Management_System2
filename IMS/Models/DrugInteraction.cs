using System.ComponentModel.DataAnnotations;

namespace IMS.Models
{
    public class DrugInteraction
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "الدواء الأول")]
        public int Product1Id { get; set; }
        public virtual Product Product1 { get; set; }

        [Required]
        [Display(Name = "الدواء الثاني")]
        public int Product2Id { get; set; }
        public virtual Product Product2 { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "مستوى الخطورة")]
        public string SeverityLevel { get; set; } // خطير، متوسط، بسيط

        [Required]
        [StringLength(1000)]
        [Display(Name = "وصف التفاعل")]
        public string InteractionDescription { get; set; }

        [StringLength(500)]
        [Display(Name = "التوصيات")]
        public string? Recommendations { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "تاريخ الإضافة")]
        public DateTime CreatedDate { get; set; } = AppClock.UtcNow;
    }
}
