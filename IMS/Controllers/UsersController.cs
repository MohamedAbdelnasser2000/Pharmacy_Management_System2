using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IMS.Models;
using IMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using IMS.Data;
using IMS.Services;

namespace IMS.Controllers
{
    [Authorize(Roles = RoleNamesConstants.Admin)]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context, IAuditService auditService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var userList = new List<UserViewModel>();

            foreach (var user in users)
            {
                var userVM = new UserViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName ?? "",
                    Email = user.Email ?? "",
                    UserName = user.UserName ?? "",
                    BranchName = user.BranchName,
                    IsActive = user.IsActive,
                    Roles = (await _userManager.GetRolesAsync(user)).ToList()
                };
                userList.Add(userVM);
            }

            return View(userList);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Roles = await _roleManager.Roles.Select(r => new SelectListItem { Value = r.Name, Text = r.Name }).ToListAsync();
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel model, string[] selectedRoles)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    BranchId = model.BranchId,
                    IsActive = model.IsActive,
                    EmailConfirmed = true,
                    CreatedDate = AppClock.UtcNow
                };

                if (model.BranchId.HasValue)
                {
                    var branch = await _context.Branches.FindAsync(model.BranchId);
                    user.BranchName = branch?.Name;
                }

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    if (selectedRoles != null && selectedRoles.Any())
                    {
                        await _userManager.AddToRolesAsync(user, selectedRoles);
                    }
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            ViewBag.Roles = await _roleManager.Roles.Select(r => new SelectListItem { Value = r.Name, Text = r.Name }).ToListAsync();
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name", model.BranchId);
            return View(model);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = await _roleManager.Roles.ToListAsync();

            var model = new EditUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName ?? "",
                Email = user.Email ?? "",
                BranchId = user.BranchId,
                BranchName = user.BranchName,
                IsActive = user.IsActive,
                Roles = userRoles.ToList()
            };

            ViewBag.Roles = allRoles.Select(r => new SelectListItem 
            { 
                Value = r.Name, 
                Text = r.Name, 
                Selected = userRoles.Contains(r.Name!) 
            }).ToList();
            
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name", user.BranchId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model, string[] selectedRoles)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(model.Id);
                if (user == null) return NotFound();

                user.Email = model.Email;
                user.UserName = model.Email;
                user.FullName = model.FullName;
                user.BranchId = model.BranchId;
                user.IsActive = model.IsActive;

                if (model.BranchId.HasValue)
                {
                    var branch = await _context.Branches.FindAsync(model.BranchId);
                    user.BranchName = branch?.Name;
                }

                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    
                    if (selectedRoles != null && selectedRoles.Any())
                    {
                        await _userManager.AddToRolesAsync(user, selectedRoles);
                    }
                    
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            ViewBag.Roles = await _roleManager.Roles.Select(r => new SelectListItem { Value = r.Name, Text = r.Name }).ToListAsync();
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name", model.BranchId);
            return View(model);
        }

        public async Task<IActionResult> ChangePassword(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var model = new ChangePasswordViewModel
            {
                Id = user.Id,
                FullName = user.FullName
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(model.Id);
                if (user == null) return NotFound();

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

                if (result.Succeeded)
                {
                    await _auditService.LogAsync("ChangePassword", "User", user.Id, null, new { FullName = user.FullName, Email = user.Email });
                    TempData["SuccessMessage"] = "تم تغيير كلمة المرور بنجاح";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            await _auditService.LogAsync("ToggleStatus", "User", user.Id, new { IsActive = !user.IsActive }, new { IsActive = user.IsActive });

            return RedirectToAction(nameof(Index));
        }
    }
}
