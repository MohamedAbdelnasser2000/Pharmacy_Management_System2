using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;

namespace IMS.Controllers
{
    [Authorize(Roles = RoleNamesConstants.Admin)]
    public class StoreSettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StoreSettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: StoreSettings
        public async Task<IActionResult> Index()
        {
            var settings = await _context.StoreSettings.FirstOrDefaultAsync();
            
            // إذا لم توجد إعدادات، إنشاء إعدادات افتراضية
            if (settings == null)
            {
                settings = new StoreSettings
                {
                    StoreName = "نظام إدارة المخازن",
                    PhoneNumber = "",
                    PhoneNumber2 = "",
                    Address = "",
                    Email = "",
                    Website = "",
                    TaxNumber = "",
                    CommercialRegister = "",
                    LogoUrl = ""
                };
                _context.StoreSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            return View(settings);
        }

        // GET: StoreSettings/Edit
        public async Task<IActionResult> Edit()
        {
            var settings = await _context.StoreSettings.FirstOrDefaultAsync();
            
            if (settings == null)
            {
                settings = new StoreSettings
                {
                    StoreName = "نظام إدارة المخازن"
                };
                _context.StoreSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            return View(settings);
        }

        // POST: StoreSettings/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(StoreSettings storeSettings, IFormFile? logoFile)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingSettings = await _context.StoreSettings.FirstOrDefaultAsync();
                    
                    // معالجة رفع الشعار
                    if (logoFile != null && logoFile.Length > 0)
                    {
                        // التحقق من نوع الملف
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".svg" };
                        var extension = Path.GetExtension(logoFile.FileName).ToLowerInvariant();
                        
                        if (!allowedExtensions.Contains(extension))
                        {
                            ModelState.AddModelError("logoFile", "يرجى رفع صورة بصيغة JPG, PNG, GIF أو SVG فقط");
                            return View(storeSettings);
                        }
                        
                        // التحقق من حجم الملف (5 ميجا كحد أقصى)
                        if (logoFile.Length > 5 * 1024 * 1024)
                        {
                            ModelState.AddModelError("logoFile", "حجم الصورة يجب أن لا يتجاوز 5 ميجابايت");
                            return View(storeSettings);
                        }
                        
                        // إنشاء اسم فريد للملف
                        var fileName = $"logo_{Guid.NewGuid()}{extension}";
                        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "logos");
                        var filePath = Path.Combine(uploadsFolder, fileName);
                        
                        // حذف الشعار القديم إذا وجد
                        if (existingSettings != null && !string.IsNullOrEmpty(existingSettings.LogoUrl))
                        {
                            var oldLogoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingSettings.LogoUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldLogoPath))
                            {
                                System.IO.File.Delete(oldLogoPath);
                            }
                        }
                        
                        // حفظ الملف الجديد
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await logoFile.CopyToAsync(stream);
                        }
                        
                        // تحديث مسار الشعار
                        storeSettings.LogoUrl = $"/uploads/logos/{fileName}";
                    }
                    else if (existingSettings != null)
                    {
                        // الاحتفاظ بالشعار القديم إذا لم يتم رفع شعار جديد
                        storeSettings.LogoUrl = existingSettings.LogoUrl;
                    }
                    
                    if (existingSettings != null)
                    {
                        // تحديث الإعدادات الموجودة
                        existingSettings.StoreName = storeSettings.StoreName;
                        existingSettings.PhoneNumber = storeSettings.PhoneNumber;
                        existingSettings.PhoneNumber2 = storeSettings.PhoneNumber2;
                        existingSettings.Address = storeSettings.Address;
                        existingSettings.Email = storeSettings.Email;
                        existingSettings.Website = storeSettings.Website;
                        existingSettings.TaxNumber = storeSettings.TaxNumber;
                        existingSettings.CommercialRegister = storeSettings.CommercialRegister;
                        existingSettings.LogoUrl = storeSettings.LogoUrl;
                        
                        _context.Update(existingSettings);
                    }
                    else
                    {
                        // إنشاء إعدادات جديدة
                        _context.Add(storeSettings);
                    }
                    
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم حفظ الإعدادات بنجاح";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    ModelState.AddModelError("", "حدث خطأ أثناء حفظ الإعدادات");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"حدث خطأ: {ex.Message}");
                }
            }
            return View(storeSettings);
        }

        // Helper method to get settings (for use in other controllers)
        public static async Task<StoreSettings> GetSettingsAsync(ApplicationDbContext context)
        {
            var settings = await context.StoreSettings.FirstOrDefaultAsync();
            
            if (settings == null)
            {
                settings = new StoreSettings
                {
                    StoreName = "نظام إدارة المخازن"
                };
            }
            
            return settings;
        }
    }
}
