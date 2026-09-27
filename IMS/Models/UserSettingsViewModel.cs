using System.ComponentModel.DataAnnotations;

namespace IMS.Models
{
    public class UserSettingsViewModel
    {
        [Display(Name = "اسم المستخدم")]
        public string UserName { get; set; }

        [Display(Name = "الاسم الكامل")]
        public string FullName { get; set; }

        [Display(Name = "البريد الإلكتروني")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        public string Email { get; set; }

        [Display(Name = "كلمة المرور الحالية")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; }

        [Display(Name = "كلمة المرور الجديدة")]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "يجب أن تكون {0} على الأقل {2} وبحد أقصى {1} حرفاً.", MinimumLength = 6)]
        public string NewPassword { get; set; }

        [Display(Name = "تأكيد كلمة المرور")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "كلمة المرور وتأكيد كلمة المرور غير متطابقتين.")]
        public string ConfirmPassword { get; set; }
    }
}
