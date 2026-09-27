using System;
using System.ComponentModel.DataAnnotations;

namespace IMS.Models
{
    public class MedicationRequest
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الدواء مطلوب")]
        [Display(Name = "اسم الدواء")]
        [StringLength(200)]
        public string MedicationName { get; set; }

        [Display(Name = "الاسم العلمي")]
        [StringLength(200)]
        public string? ScientificName { get; set; }

        [Display(Name = "التركيز")]
        [StringLength(100)]
        public string? Strength { get; set; }

        [Display(Name = "الشكل الدوائي")]
        [StringLength(100)]
        public string? DosageForm { get; set; }

        [Display(Name = "اسم المريض")]
        [StringLength(200)]
        public string? PatientName { get; set; }

        [Display(Name = "رقم هاتف المريض")]
        [StringLength(50)]
        public string? PatientPhone { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "الكمية المطلوبة")]
        public int? RequestedQuantity { get; set; }

        [Display(Name = "الحالة")]
        [Required]
        public RequestStatus Status { get; set; } = RequestStatus.Pending;

        [Display(Name = "تاريخ الطلب")]
        [Required]
        public DateTime RequestDate { get; set; } = AppClock.UtcNow;

        [Display(Name = "تاريخ الطلب من المورد")]
        public DateTime? OrderedDate { get; set; }

        [Display(Name = "تاريخ الاستلام")]
        public DateTime? ReceivedDate { get; set; }

        [Display(Name = "تم إبلاغ المريض")]
        public bool PatientNotified { get; set; }

        [Display(Name = "الأولوية")]
        public Priority Priority { get; set; } = Priority.Normal;

        [Display(Name = "المستخدم")]
        public string? CreatedBy { get; set; }

        [Display(Name = "الفرع")]
        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }
    }

    public enum RequestStatus
    {
        [Display(Name = "قيد الانتظار")]
        Pending = 0,
        
        [Display(Name = "تم الطلب من المورد")]
        Ordered = 1,
        
        [Display(Name = "تم الاستلام")]
        Received = 2,
        
        [Display(Name = "تم التسليم للمريض")]
        Delivered = 3,
        
        [Display(Name = "ملغي")]
        Cancelled = 4
    }

    public enum Priority
    {
        [Display(Name = "عادي")]
        Normal = 0,
        
        [Display(Name = "عاجل")]
        Urgent = 1,
        
        [Display(Name = "طارئ")]
        Emergency = 2
    }
}
