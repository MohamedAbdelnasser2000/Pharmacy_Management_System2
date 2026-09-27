using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using IMS.Models;

namespace IMS.Controllers
{
    [Authorize]
    public class UserSettingsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public UserSettingsController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // GET: UserSettings
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new UserSettingsViewModel
            {
                UserName = user.UserName,
                FullName = user.FullName,
                Email = user.Email
            };

            return View(model);
        }

        // POST: UserSettings/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UserSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            bool updated = false;

            // Update username
            if (!string.IsNullOrEmpty(model.UserName) && model.UserName != user.UserName)
            {
                var setUserNameResult = await _userManager.SetUserNameAsync(user, model.UserName);
                if (!setUserNameResult.Succeeded)
                {
                    foreach (var error in setUserNameResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View("Index", model);
                }
                updated = true;
            }

            // Update full name
            if (model.FullName != user.FullName)
            {
                user.FullName = model.FullName;
                updated = true;
            }

            // Update email
            if (!string.IsNullOrEmpty(model.Email) && model.Email != user.Email)
            {
                var setEmailResult = await _userManager.SetEmailAsync(user, model.Email);
                if (!setEmailResult.Succeeded)
                {
                    foreach (var error in setEmailResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View("Index", model);
                }
                updated = true;
            }

            if (updated)
            {
                await _userManager.UpdateAsync(user);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["SuccessMessage"] = "تم تحديث البيانات بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // POST: UserSettings/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(UserSettingsViewModel model)
        {
            if (string.IsNullOrEmpty(model.CurrentPassword) || 
                string.IsNullOrEmpty(model.NewPassword) || 
                string.IsNullOrEmpty(model.ConfirmPassword))
            {
                TempData["ErrorMessage"] = "جميع حقول كلمة المرور مطلوبة";
                return RedirectToAction(nameof(Index));
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                TempData["ErrorMessage"] = "كلمة المرور الجديدة وتأكيد كلمة المرور غير متطابقتين";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(
                user, 
                model.CurrentPassword, 
                model.NewPassword);

            if (!changePasswordResult.Succeeded)
            {
                var errors = string.Join(", ", changePasswordResult.Errors.Select(e => e.Description));
                TempData["ErrorMessage"] = "فشل تغيير كلمة المرور: " + errors;
                return RedirectToAction(nameof(Index));
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["SuccessMessage"] = "تم تغيير كلمة المرور بنجاح";
            return RedirectToAction(nameof(Index));
        }
    }
}
