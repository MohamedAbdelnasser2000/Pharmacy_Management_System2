using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Areas.ECommerce.Controllers
{
    [Area("ECommerce")]
    [Authorize(Roles = RoleNamesConstants.Admin)]
    public class ECommerceSettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ECommerceSettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var settings = await _context.ECommerceSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new ECommerceSettings();
                _context.ECommerceSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ECommerceSettings model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var settings = await _context.ECommerceSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                _context.ECommerceSettings.Add(model);
            }
            else
            {
                settings.BannerTitle = model.BannerTitle;
                settings.BannerSubtitle = model.BannerSubtitle;
                settings.IsStoreActive = model.IsStoreActive;
                settings.RequireLoginToPurchase = model.RequireLoginToPurchase;
                settings.FreeShippingThreshold = model.FreeShippingThreshold;
                settings.DefaultShippingFee = model.DefaultShippingFee;
                settings.TermsOfService = model.TermsOfService;
                settings.AnnouncementMessage = model.AnnouncementMessage;
                settings.ShowPrescriptionProducts = model.ShowPrescriptionProducts;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم تحديث إعدادات المتجر الإلكتروني بنجاح";
            return RedirectToAction("Index");
        }
    }
}
